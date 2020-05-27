# Tutorial 3: Erasing Strokes

This tutorial demonstrates how strokes can be used for manipulation purposes instead of rendering. 
The examples implement two types of "eraser tools" - the first one erasing whole strokes, and the second one - erasing only parts of the strokes.

* [Part 1: Extending the Stroke Model](#part-1-extending-the-stroke-model)
* [Part 2: Erasing Strokes](#part-2-erasing-strokes)
* [Part 3: Erasing Stroke Parts](#part-3-erasing-stroke-parts)

---
## Part 1: Extending the Stroke Model

In this example we will extend the functionality of the Stroke class from Tutorial 2 - this will help us with the utilization of WILL SDK's manipulation features.

The Stroke class should be able to calculate axis aligned bounding boxes for the individual path segments as well as a bounding box for the whole path.

First we declare the following fields in order to store the calculated bounding boxes:

```csharp
        // bounds cache
        private List<Rect> _cachedSegmentsBounds;
        private Rect? _cachedBounds;
```

#We will define a GetBounds method to perform the actual calculation of the bounding boxes. 
The implementation is pretty straightforward: first we obtain the number of segments in the path using the PathUtils.CalculateSegmentBounds method, then the bounding box of each segment is calculated with PathUtils.CalculateSegmentBounds and added to a list. 
At the same time, the tmpBounds variable is used to store the union of the bounding boxes - at the end of the loop this will be the overall bounding box of the path.

```csharp
        public List<Rect> GetBounds(out Rect bounds)
        {
            if (!_cachedBounds.HasValue)
            {
                Rect tmpBounds;
                List<Rect> tmpSegmentsBounds;

                if ((_path != null) && (_path.PointsCount > 0))
                {
                    int segmentsCount = _path.SegmentsCount;

                    if (segmentsCount > 0)
                    {
                        tmpBounds = _path.CalculateSegmentBounds(0, this.Width, 0.0f);
                        tmpSegmentsBounds = new List<Rect>();
                        tmpSegmentsBounds.Add(tmpBounds);

                        for (int i = 1; i < segmentsCount; i++)
                        {
                            Rect rc = _path.CalculateSegmentBounds(i, this.Width, 0.0f);
                            tmpBounds.Union(rc);
                            tmpSegmentsBounds.Add(rc);
                        }

                        _cachedBounds = tmpBounds;
                        _cachedSegmentsBounds = tmpSegmentsBounds;
                    }
                }
            }

            if (!_cachedBounds.HasValue)
                return null;

            bounds = _cachedBounds.Value;
            return _cachedSegmentsBounds;
        }
```

In the following parts of this tutorial the bounding boxes will be supplied to WILL SDK methods that perform intersections of strokes.

---
## Part 2: Erasing Strokes
This part of the tutorial demonstrates how to delete existing strokes interactively by intersecting them with a stroke that is built "on the fly" from pointer input. 
The erasing stroke will not be rendered, but only used for manipulation of the other strokes, thus creating the illusion that the user is erasing the strokes with his finger or pointing device.

Since the focus of the example is on erasing strokes, we will first load existing strokes from a file (like in Tutorial 2, Part 2). 
After the ink rendering engine is initialized, we load some strokes using the StrokeDecoder class. 
Finally we call the DrawAllStrokes method in order to draw the loaded strokes on the screen.

```csharp
        async Task LoadStrokes()
        {
            StorageFile storageFile = await StorageFile.GetFileFromApplicationUriAsync(new Uri(@"ms-appx:///Assets/strokes.bin"));

            if (storageFile != null)
            {
                IBuffer buffer = await FileIO.ReadBufferAsync(storageFile);

                using (StrokeDecoder decoder = new StrokeDecoder(buffer))
                {
                    while (decoder.MoveNext())
                    {
                        StrokeData strokeData = decoder.DecodeCurrent();
                        _strokes.Add(new Stroke(strokeData));
                    }
                }
            }

            DrawAllStrokes();
        }
```


The input handling is almost the same as in the previous tutorials - in general we have to supply pointer points to the PathBuilder to build a path. 
The essential difference is that now we are not rendering the current stroke directly, but we intersect it with the existing strokes instead.

```csharp
        void OnPointerInputBegin(PointerRoutedEventArgs e)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (_pointerId.HasValue)
                return;

            // Capture the pointer and store its Id
            this.DxPanel.CapturePointer(e.Pointer);
            _pointerId = e.Pointer.PointerId;

            // Reset the state related to path building
            _updateFromIndex = -1;

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.Begin, e);

            IntersectStrokes();
        }

        void OnPointerInputMove(PointerRoutedEventArgs e)
        {
            // Ignore events from other pointers
            if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
                return;

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.Move, e);

            IntersectStrokes();
        }

        void OnPointerInputEnd(PointerRoutedEventArgs e)
        {
            // Ignore events from other pointers
            if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
                return;

            // Reset the stored id and release the pointer capture
            _pointerId = null;
            this.DxPanel.ReleasePointerCapture(e.Pointer);

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.End, e);

            IntersectStrokes();
        }
```

The IntersectStrokes method intersects the "eraser" stroke with each of the strokes in the _strokes collection. 
If there is an intersection, the intersected stroke is removed from the strokes collection. 
The object that performs the intersection is part of the WILL SDK and is of type Intersector. 
The erasing stroke is passed to the Intersector object using the SetTargetAsStroke method. 
Then each stroke is tested upon the erasing stroke using the IsIntersectingTarget method. 
If the intersection test returns true, the tested stroke is removed from the _strokes list.

```csharp
        void IntersectStrokes()
        {
            if (_strokes.Count == 0)
                return;

            if (_updateFromIndex < 0)
                return;

            Path currentPath = _pathBuilder.CurrentPath;

            int numberOfPointsToUpdate = currentPath.PointsCount - _updateFromIndex;
            if (numberOfPointsToUpdate <= 0)
                return;

            bool mustRedrawStrokes = false;

            using (Intersector intersector = new Intersector())
            {
                intersector.SetTargetAsStroke(currentPath, _updateFromIndex, numberOfPointsToUpdate, null);

                _updateFromIndex = -1;

                for (int i = _strokes.Count - 1; i >= 0; i--)
                {
                    Stroke s = _strokes[i];

                    Rect strokeBounds;
                    List<Rect> segmentsBounds = s.GetBounds(out strokeBounds);

                    if (segmentsBounds == null)
                        continue;

                    if (intersector.IsIntersectingTarget(s.Path, 0, s.Path.PointsCount, s.Width, s.Ts, s.Tf, strokeBounds, segmentsBounds))
                    {
                        _strokes.RemoveAt(i);
                        mustRedrawStrokes = true;
                    }
                }
            }

            if (mustRedrawStrokes)
            {
                DrawAllStrokes();
            }
        }
```

It is important to notice that we do not set the whole currentPath as intersector target, but only the latest portion of it. 
The concept is similar to the stroke rendering approach that we use in the previous tutorials, where only the latest portion of the current stroke is rendered on each step. 
Using the whole current stroke as intersector target should be avoided, because this will degrade the intersector's performance.

You may have noticed that the IsIntersectingTarget method requires the bounding boxes of the stroke and the stroke segments. 
We obtain them using the stroke's GetBounds method - the one that we added in Part 1 of this tutorial.

At the end, if there are changes in the strokes collection, the IntersectStrokes method calls DrawAllStrokes to update the display. 
DrawAllStrokes simply clears the strokes layer and draws all of the strokes in it. 
The strokes layer is copied to the backbuffer layer and the scene is presented.

```csharp
        void DrawAllStrokes()
        {
            _renderingContext.SetTarget(_strokesLayer);
            _renderingContext.ClearColor(_backgroundColor);

            for (int i = 0; i < _strokes.Count; i++)
            {
                Stroke s = _strokes[i];

                _strokeRenderer.Color = s.Color;
                _strokeRenderer.Ts = s.Ts;
                _strokeRenderer.Tf = s.Tf;

                _strokeRenderer.ResetAndClear();
                _strokeRenderer.DrawStroke(s.Path, 0, s.Path.PointsCount, true);
                _strokeRenderer.BlendStrokeInLayer(_strokesLayer, BlendMode.Normal);
            }

            // copy the strokes layer to the backbuffer layer
            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);

            // present
            _graphics.Present();
        }
```

The next part of the tutorial will demonstrate how to erase portions of a stroke using the Intersector class.

---
## Part 3: Erasing Stroke Parts

Now we will modify the code from the previous example in such a way that the user will be able to erase interactively parts of existing strokes instead of whole strokes. 
Like in the previous part of the tutorial, the erasing stroke will not be rendered, but only used for manipulation of the visible strokes. 
The examples are very similar, therefore only the differences will be highlighted.

The essence of Part 2 was the Intersector's IsIntersectingTarget method, which reports whether two strokes intersect. 
In this part we will use another method of the intersector - IntersectWithTarget. 
It returns an object of type IntersectionResult, which contains detailed information about the intersections of the target stoke and the tested stroke.

The IntersectionResult can be regarded as an array of intervals in the tested path, where each interval is inside or outside the intersection target. 
The actual form of the result is slightly different - it consists of three arrays that contain respectively begin / end point indices, begin / end T values and "Inside" flags. 
In order to make the usage of the IntersectionResult easier, we will create a helper type Interval and an indexable list of intervals. 
This way we will be able to work with the IntersectionResult as if it were a real collection of intervals.

```csharp
    struct Interval
    {
        public int FromIndex;
        public int ToIndex;
        public float FromTValue;
        public float ToTValue;
        public bool Inside;

        public int GetSize()
        {
            return ToIndex - FromIndex + 1;
        }
    }
```

The IntervalList will be initialized from an IntersectionResult. 
The number of values in the Inside array is equal to the number of intervals. 
The Indices and TValues arrays should be exactly twice larger, because each of them contains two values per interval. 
The IntervalList's indexer constructs an Interval for the specified index by picking the correct values from the Indices, TValues and Inside arrays of the IntersectionResult.

```csharp
    class IntervalList
    {
        private IntersectionResult _result;
        private int _count;

        public IntervalList(IntersectionResult result)
        {
            _result = result;
            _count = _result.Inside.Count;

            Debug.Assert(_count * 2 == _result.Indices.Count);
            Debug.Assert(_result.Indices.Count == _result.TValues.Count);
        }

        public int Count
        {
            get
            {
                return _count;
            }
        }
        public Interval this[int index]
        {
            get
            {
                int index1 = index * 2;
                int index2 = index1 + 1;

                Interval interval;
                interval.FromIndex = _result.Indices[index1];
                interval.ToIndex = _result.Indices[index2];
                interval.FromTValue = _result.TValues[index1];
                interval.ToTValue = _result.TValues[index2];
                interval.Inside = _result.Inside[index] == 1;

                return interval;
            }
        }
    }
```

We need to upgrade our Stroke class by adding a constructor that initializes a stroke from an interval of another Stroke. 
The srcStroke parameter denotes the source stroke while the interval parameter specifies the interval. 
Only the values within the interval are copied from the source path to the new path.

```csharp
        public Stroke(Stroke srcStroke, Interval interval)
        {
            int stride = (int)srcStroke.Path.DataStride;
            int fromIndex = stride * interval.FromIndex;
            int toIndex = stride * (interval.ToIndex + 1);

            List<float> pathData = new List<float>();

            for (int i = fromIndex; i < toIndex; i++)
            {
                pathData.Add(srcStroke.Path.Data[i]);
            }

            _path = new Path(pathData, srcStroke.Path.DataStride);

            this.Width = srcStroke.Width;
            this.Color = srcStroke.Color;
            this.Ts = interval.FromTValue;
            this.Tf = interval.ToTValue;
        }
```

In order to make erasing easier for the user, we increase the width of the erasing stroke by specifying larger values in the Width property configuration:

```csharp
        _pathBuilder.SetPropertyConfig(PropertyName.Width, 50.0f, 60.0f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);
```

At this point we are ready to modify the implementation the IntersectStrokes method. 
In fact there are only two differences from the IntersectStrokes Part 2: First, as already mentioned, IntersectWithTarget is called instead of IsIntersectingTarget. 
Second, instead of directly deleting the intersected stroke, we call the ProcessStroke method, which implements a slightly more complex logic.

```csharp
        void IntersectStrokes()
        {
            if (_strokes.Count == 0)
                return;

            if (_updateFromIndex < 0)
                return;

            Path currentPath = _pathBuilder.CurrentPath;

            int numberOfPointsToUpdate = currentPath.PointsCount - _updateFromIndex;
            if (numberOfPointsToUpdate <= 0)
                return;

            bool mustRedrawStrokes = false;

            using (Intersector intersector = new Intersector())
            {
                intersector.SetTargetAsStroke(currentPath, _updateFromIndex, numberOfPointsToUpdate, null);

                _updateFromIndex = -1;

                for (int i = _strokes.Count - 1; i >= 0; i--)
                {
                    Stroke s = _strokes[i];

                    Rect strokeBounds;
                    List<Rect> segmentsBounds = s.GetBounds(out strokeBounds);

                    if (segmentsBounds == null)
                        continue;

                    using (IntersectionResult result = intersector.IntersectWithTarget(s.Path, 0, s.Path.PointsCount, s.Width, s.Ts, s.Tf, strokeBounds, segmentsBounds))   
                    {
                        ProcessStroke(s, i, result, ref mustRedrawStrokes);
                    }
                }
            }

            if (mustRedrawStrokes)
            {
                DrawAllStrokes();
            }
        }
```

The ProcessStroke method is the place where the Interval and IntervalList types are utilized. 
In the general case the method removes the original stroke and replaces it with several new strokes - one for each "outside" interval.

```csharp
        void ProcessStroke(Stroke s, int strokeIndex, IntersectionResult intersectionResult, ref bool mustRedrawStrokes)
        {
            IntervalList intervals = new IntervalList(intersectionResult);

            if (intervals.Count == 1)
            {
                if (intervals[0].Inside)
                {
                    // delete the whole stroke
                    _strokes.RemoveAt(strokeIndex);
                    mustRedrawStrokes = true;
                }
                else
                {
                    // do nothing - keep the original stroke
                }
            }
            else
            {
                _strokes.RemoveAt(strokeIndex);
                mustRedrawStrokes = true;

                for (int k = 0; k < intervals.Count; k++)
                {
                    Interval interval = intervals[k];

                    if (interval.Inside == false)
                    {
                        _strokes.Insert(strokeIndex, new Stroke(s, interval));
                    }
                }
            }
        }
```

Like in Part 2, the scene should be repainted only when there are actual changes in the _strokes collection. 
For this purpose, the out parameter mustRedrawStrokes reports to the caller whether a repaint is necessary.

---
