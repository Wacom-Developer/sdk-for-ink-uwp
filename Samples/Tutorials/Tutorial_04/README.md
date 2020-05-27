# Tutorial 4: Selecting Strokes

This tutorial demonstrates how to implement a lasso-like tool for selection of strokes. 
Similarly to the previous tutorial, there are two modifications of the tool - one selecting whole strokes, and the other selecting only portions of the strokes.

* [Part 1: Selecting Strokes](#part-1-selecting-strokes)
* [Part 2: Selecting Stroke Parts](#part-2-selecting-stroke-parts)

---
## Part 1: Selecting Strokes

This tutorial demonstrates how to implement a lasso tool for selection of whole strokes. 
The concept is similar to Tutorial 3, Part 2, where we were erasing whole strokes. 
Once again we will manipulate existing strokes loaded from a file.

There are several notable differences between the eraser and the selector:

* The selecting stroke should be visible.
* The selecting stroke should be a thin line with constant width.
* The selector must act as a lasso - it must define a closed path and the strokes that are within the path are considered as selected.

To meet these requirements, we have to configure our path builder to produce strokes with constant width. 
We will also create a smoothener for the selecting stroke.

```csharp
        void CreateWacomInkObjects()
        {
            // Create a graphics object
            _graphics = new Graphics();

            // Create a path builder for constant stroke width
            _pathBuilder = new SpeedPathBuilder();
            _pathBuilder.SetMovementThreshold(0.1f);

            // Create an object that smooths input data
            _smoothener = new MultiChannelSmoothener(_pathBuilder.PathStride);

            ...
        }
```

The rendering of the current stroke is the same as in Tutorial 1, Part 6. 
We use a scene layer (_sceneLayer), in which we draw the scene including the selector stroke. 
The strokes that are being manipulated are stored in the _strokesLayer.

When the user starts a new selector stroke, we have to setup the stroke renderer for rendering that stroke. 
This includes setting the StrokeWidth property to a constant value.

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
        _pathFinished = false;

        // Reset the smoothener
        _smoothener.Reset();

        // Reset the stroke renderer and prepare it for selector stroke rendering
        _strokeRenderer.ResetAndClear();
        _strokeRenderer.Color = Colors.Red;
        _strokeRenderer.StrokeWidth = 1.0f;
        _strokeRenderer.Ts = 0.0f;
        _strokeRenderer.Tf = 1.0f;

        // Add the pointer point to the path builder
        AddCurrentPointToPathBuilder(InputPhase.Begin, e);

        DrawCurrentStroke();
    }
```

When the selecting stroke is finished we have to check which are the selected strokes and update the scene accordingly. 
If there are no selected strokes, we copy the _strokesLayer to the _sceneLayer, thus removing the selector stroke from the scene.

```csharp
        void OnPointerInputEnd(PointerRoutedEventArgs e)
        {
            // Ignore events from other pointers
            if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
                return;

            // Reset the stored id and release the pointer capture
            _pointerId = null;
            this.DxPanel.ReleasePointerCapture(e.Pointer);

            AddCurrentPointToPathBuilder(InputPhase.End, e);

            _pathFinished = true;
            _updateFromIndex = 0;

            if (IntersectStrokes())
            {
                DrawAllStrokes();
            }
            else
            {
                _renderingContext.SetTarget(_sceneLayer);
                _renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);
            }

            Present();
        }
```

The DrawAllStrokes method clears the _strokesLayer and draws all the strokes from the _strokes collection in it.

```csharp
        void DrawAllStrokes()
        {
            _renderingContext.SetTarget(_strokesLayer);
            _renderingContext.ClearColor(_backgroundColor);

            _strokeRenderer.StrokeWidth = null;

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

            _renderingContext.SetTarget(_sceneLayer);
            _renderingContext.DrawLayer(_strokesLayer, null, BlendMode.None);
        }
```

In order to find out which strokes are selected we will use the Intersector type from WILL SDK. 
Unlike Tutorial 3, we will specify the intersetor target with the SetTargetAsClosedPath method, thus instructing the intersector to treat the stroke as a closed path. 
This time we are setting the whole path as intersector target.

```csharp
        bool IntersectStrokes()
        {
            if (_strokes.Count == 0)
                return false;

            Path currentPath = _pathBuilder.CurrentPath;

            if (currentPath.PointsCount <= 0)
                return false;

            bool mustRedrawStrokes = false;

            using (Intersector intersector = new Intersector())
            {
                intersector.SetTargetAsClosedPath(currentPath, 0, currentPath.PointsCount);

                for (int i = _strokes.Count - 1; i >= 0; i--)
                {
                    Stroke s = _strokes[i];

                    Rect strokeBounds;
                    List<Rect> segmentsBounds = s.GetBounds(out strokeBounds);

                    if (segmentsBounds == null)
                        continue;

                    if (intersector.IsIntersectingTarget(s.Path, 0, s.Path.PointsCount, s.Width, s.Ts, s.Tf, strokeBounds, segmentsBounds))
                    {
                        s.InvertColor();
                        mustRedrawStrokes = true;
                    }
                }
            }

            return mustRedrawStrokes;
        }
```

The IsIntersectingTarget method will return true for each stroke that intersects the closed path. 
Whenever this happens we invert the stroke's color to indicate that the stroke is selected.

---
## Part 2: Selecting Stroke Parts

Now we will modify the code from the previous example in such a way that the user will be able to select parts of existing strokes instead of whole strokes. 
The examples are very similar, so only the differences will be highlighted.

Instead of the IsIntersectingTarget method, we will use the IntersectWithTarget method, which was introduces in Tutorial 3, Part 3. 
IntersectWithTarget will return detailed information about the intersections between the selecting stroke and the tested strokes, which will help us select only certain portions of the strokes.

```csharp
        bool IntersectStrokes()
        {
            if (_strokes.Count == 0)
                return false;

            Path currentPath = _pathBuilder.CurrentPath;

            if ((currentPath.PointsCount <= 0))
                return false;

            bool mustRedrawStrokes = false;

            using (Intersector intersector = new Intersector())
            {
                intersector.SetTargetAsClosedPath(currentPath, 0, currentPath.PointsCount);

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

            return mustRedrawStrokes;
        }
```

For each of the tested strokes we call the ProcessStroke method, which implements the partial selection. 
In the general case the method removes the original stroke and replaces it with several new strokes - one for each interval in the intersection result. 
For the "inside" intervals we create new strokes with an inverted color, thus indicating that they are selected. 
For the "outside" intervals we create strokes with the original color.

```csharp
        void ProcessStroke(Stroke s, int strokeIndex, IntersectionResult intersectionResult, ref bool mustRedrawStrokes)
        {
            IntervalList intervals = new IntervalList(intersectionResult);

            if (intervals.Count == 1)
            {
                if (intervals[0].Inside)
                {
                    // invert the whole stroke
                    s.InvertColor();
                    mustRedrawStrokes = true;
                }
                else
                {
                    // do nothing - keep the original stroke color
                }
            }
            else
            {
                _strokes.RemoveAt(strokeIndex);
                mustRedrawStrokes = true;

                for (int k = 0; k < intervals.Count; k++)
                {
                    Interval interval = intervals[k];

                    Stroke newStroke = new Stroke(s, interval);

                    if (interval.Inside)
                    {
                        newStroke.InvertColor();
                    }

                    _strokes.Insert(strokeIndex, newStroke);
                }
            }
        }
```

The IntervalList and Interval types used above are explained in Tutorial 3, Part 3.

---

