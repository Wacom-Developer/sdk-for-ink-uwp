# Tutorial 2: Stroke Model and Serialization

This tutorial demonstrates how to create a simple stroke model class and use it for path building, rendering and serialization. 
The applications demonstrated in Part 2 and Part 3 are able to display multiple strokes at the same time, save the currently displayed strokes to a file and load them back.

* [Part 1: Stroke Model](#part-1-stroke-model)
* [Part 2: Stroke Serialization](#part-2-stroke-serialization)
* [Part 3: WILL format](#part-3-will-format)

---
## Part 1: Stroke Model

In the previous tutorial we used the term "stroke", but there was no such abstraction in the code. 
Now we will create a type that encapsulates the stroke related data in order to simplify the code in the subsequent examples.

Let's call the new type Stroke and declare the following fields and properties:

```csharp
        internal class Stroke
        {
            private Path _path;

            public Path Path
            {
                get
                {
                    return _path;
                }
            }

            public float? Width { get; set; }
            public Color Color { get; set; }
            public float Ts { get; set; }
            public float Tf { get; set; }

            ...
        }
```

These properties, along with the control path are required for stroke rendering.

We will need a constructor that will initialize the stroke with a specified color and a path. 
The path that we will normally pass to this constructor will be the pathbuilder's built-in path, so we create a copy of the passed path instead of just keeping a reference to it.

```csharp
        public Stroke(Path srcPath, Color color)
        {
            // Make a copy of the source path
            IList<float> srcPathData = srcPath.Data;
            List<float> pathData = new List<float>();

            for (int i = 0; i < srcPathData.Count; i++)
            {
                pathData.Add(srcPathData[i]);
            }

            _path = new Path(pathData, srcPath.DataStride);

            this.Width = null;
            this.Color = color;
            this.Ts = 0.0f;
            this.Tf = 1.0f;
        }
```

The second constructor will be used when creating strokes from deserialized stroke data:

```csharp
        public Stroke(StrokeData strokeData)
        {
            _path = strokeData.Path;

            this.Width = strokeData.Width;
            this.Color = strokeData.Color;
            this.Ts = strokeData.Ts;
            this.Tf = strokeData.Tf;
        }
```

In the next part of the tutorial we will integrate the Stroke class in the example code.

---
## Part 2: Stroke Serialization

In Part 1 we introduced the Stroke class that will help us demonstrate the serialization / deserialization features included in WILL SDK. 
Now we are going to demonstrate how to save stroke data to a binary stream and load it in order to draw the saved strokes on the screen.

This tutorial is based on the code from Tutorial 1, Part 6. 
A few changes are necessary in order to integrate the Stroke class in the code.

We need to declare a list of Stroke objects that will contain the data for the strokes displayed on the screen.

```csharp
        private List<Stroke> _strokes = new List<Stroke>();
```

When a stroke is finished, we have to create a corresponding Stroke object and add it to the list of strokes. 
Note that we pass the current path of the _pathBuilder to the Stroke constructor and it creates a copy of this path.

```csharp
        void OnPointerInputEnd(PointerRoutedEventArgs e)
        {
            ...

            // Store the current stroke
            _strokes.Add(new Stroke(_pathBuilder.CurrentPath, _strokeRenderer.Color));
        }
```

At this point the application are able to draw strokes and retain them in the _strokes collection. 
We will add some functionality to save the retained strokes to a file on a button click. 
For this purpose we use the StrokeEncoder type from the WILL SDK. 
For each stroke in the _strokes collection we call the StrokeEncoder's Encode method, passing the relevant data. 
When done, we call the encoder's GetData method to obtain an IBuffer, which we save to a file.

```csharp
        async void ButtonSave_Clicked(object sender, RoutedEventArgs e)
        {
            // save the finished strokes
            using (StrokeEncoder encoder = new StrokeEncoder())
            {
                foreach (Stroke s in _strokes)
                {
                    encoder.Encode(2, s.Path, s.Width, s.Color, s.Ts, s.Tf, -1, 0, BlendMode.Normal);
                }

                IBuffer buffer = encoder.GetData();

                if (buffer != null)
                {
                    StorageFile storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(_fileName, CreationCollisionOption.ReplaceExisting);

                    if (storageFile != null)
                    {
                        await FileIO.WriteBufferAsync(storageFile, buffer);
                    }
                }
            }
        }
```

In order to load the strokes back from the file we have to use the StrokeDecoder class. 
The StrokeDecoder's DecodeCurrent method will return a StrokeData object for each stroke. 
We can create Stroke objects directly from the StrokeData objects using one of the Stroke class's constructors. 
We add the newly created Stroke objects to the _strokes list and also to a temporary list that will be used for rendering of the loaded strokes:

```csharp
        async void ButtonLoad_Clicked(object sender, RoutedEventArgs e)
        {
            StorageFile storageFile = null;

            try
            {
                storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync(_fileName);
            }
            catch (System.IO.FileNotFoundException)
            {
            }

            List<Stroke> loadedStrokes = new List<Stroke>();

            if (storageFile != null)
            {
                IBuffer buffer = await FileIO.ReadBufferAsync(storageFile);

                using (StrokeDecoder decoder = new StrokeDecoder(buffer))
                {
                    while (decoder.MoveNext())
                    {
                        StrokeData strokeData = decoder.DecodeCurrent();
                        Stroke stroke = new Stroke(strokeData);

                        loadedStrokes.Add(stroke);
                        _strokes.Add(stroke);
                    }
                }
            }

            if (loadedStrokes.Count > 0)
            {
                _renderingContext.SetTarget(_allStrokesLayer);

                foreach (Stroke s in loadedStrokes)
                {
                    _strokeRenderer.Color = s.Color;
                    _strokeRenderer.Ts = s.Ts;
                    _strokeRenderer.Tf = s.Tf;

                    // draw the stroke in the strokes layer
                    _strokeRenderer.ResetAndClear();
                    _strokeRenderer.DrawStroke(s.Path, 0, s.Path.PointsCount, true);
                    _strokeRenderer.BlendStrokeInLayer(_allStrokesLayer, BlendMode.Normal);
                }

                // copy the strokes layer to the scene layer
                _renderingContext.SetTarget(_sceneLayer);
                _renderingContext.DrawLayer(_allStrokesLayer, null, BlendMode.None);

                // copy the strokes layer to the backbuffer layer
                _renderingContext.SetTarget(_backbufferLayer);
                _renderingContext.DrawLayer(_allStrokesLayer, null, BlendMode.None);

                // present
                _graphics.Present();
            }
        }
```

We render the loaded strokes into the _allStrokesLayer - after that it contains all previous strokes as well as all the newly loaded strokes. 
Note that the _allStrokesLayer layer is not cleared before the strokes are rendered and similarly the _strokes collection is not cleared before the Stroke instances are added to it. 
This is necessary to keep the layer and the collection synchronized.

We copy the _allStrokesLayer to the _sceneLayer to keep it up to date and copy the _allStrokesLayer to the _backbufferLayer in order to present the scene.

When we need to clear the strokes we use the following code:

```csharp
        void ButtonClear_Clicked(object sender, RoutedEventArgs e)
        {
            // clear the strokes collection
            _strokes.Clear();

            // clear the scene layer
            _renderingContext.SetTarget(_sceneLayer);
            _renderingContext.ClearColor(_backgroundColor);

            _renderingContext.SetTarget(_allStrokesLayer);
            _renderingContext.ClearColor(_backgroundColor);

            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.ClearColor(_backgroundColor);

            _graphics.Present();
        }
```

We maintain integrity by clearing both the _strokes collection and layers.

---
## Part 3: WILL format

WILL SDK provides a file format for serialization of stroke data. 
The format is called WILL and is based on the OPC specification.

The main functionality in regards to manipulation of WILL files is provided by the WillDocument type. 
A WillDocument can contain one or more sections. 
Each section is a hierarchical structure consisting of groups, strokes, images and other graphical elements.

In order to store stroke data in a WILL file, first we have to encode it into binary form just like in the previous part of the tutorial.

```csharp
        async void ButtonSave_Clicked(object sender, RoutedEventArgs e)
        {
            IBuffer strokesBuffer;

            // serialize the strokes to a buffer
            using (StrokeEncoder encoder = new StrokeEncoder())
            {
                foreach (Stroke s in _strokes)
                {
                    encoder.Encode(2, s.Path, s.Width, s.Color, s.Ts, s.Tf, null, null, BlendMode.Normal);
                }

                strokesBuffer = encoder.GetData();
            }

            ...
```

The next step is to create and configure a WillDocument instance. 
The WillDocument type exposes several properties that let you store metadata like document title, last modified date, application name etc.

```csharp
            ...

            // Create a WILL document
            WillDocument willDoc = new WillDocument();
            willDoc.Modified = DateTime.Now;
            willDoc.Title = "Willx Doc 1";
            willDoc.Application = "Tutorial 2";
            willDoc.AppVersion = "1.0.0";

            ...
```

Next we create a document section that will represent the scene.

```csharp
            ...

            // Create a document section
            Section section = new Section();
            willDoc.AddSection(section);

            ...
```

We add an image element and assign the encoded stroke data to it.

```csharp
            ...

            // Add a PathsElement to the section and specify that the strokes buffer contains the data for the paths.
            PathsElement paths = new PathsElement();
            section.AppendChild(paths);
            paths.PathsDataProvider = new BufferDataProvider(strokesBuffer, DataFormat.ProtobufStrokes);

            ...
```

The final step is to save the WILL document to a storage file.

```csharp
            ...

            // Save the WILL document into a storage file.
            StorageFile storageFile = await ApplicationData.Current.LocalFolder.CreateFileAsync(_fileName, CreationCollisionOption.ReplaceExisting);

            await willDoc.SaveToFileAsync(storageFile);
        }
```

In order to read the scene from an existing file we have to use the LoadFromFile method.

```csharp
        async void ButtonLoad_Clicked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Create a WILL document and load it from a storage file.
                StorageFile storageFile = await ApplicationData.Current.LocalFolder.GetFileAsync(_fileName);

                WillDocument willDoc = new WillDocument();
                await willDoc.LoadFromFileAsync(storageFile);

                ...
```

Since we know that the strokes are stored in the first image element of the first section, we search directly for this element and decode the strokes from the data assigned to it. 
The stroke data is provided in the form of a buffer that we have to pass to the StrokeDecoder class.

```csharp
                ...

                List<Stroke> loadedStrokes = new List<Stroke>();

                // Get the first section and load the strokes from it.
                Section section = willDoc.Sections.FirstOrDefault();

                if (section != null)
                {
                    PathsElement pathsElement = section.Elements.FirstOrDefault() as PathsElement;

                    if (pathsElement != null)
                    {
                        IBuffer buffer = await pathsElement.PathsDataProvider.GetDataAsync();

                        if (buffer != null)
                        {
                            using (StrokeDecoder decoder = new StrokeDecoder(buffer))
                            {
                                while (decoder.MoveNext())
                                {
                                    StrokeData strokeData = decoder.DecodeCurrent();
                                    Stroke stroke = new Stroke(strokeData);

                                    loadedStrokes.Add(stroke);
                                    _strokes.Add(stroke);
                                }
                            }
                        }
                    }
                }

                ...
```

If we manage to load some strokes from the file, we render them to the screen using the code below.

```csharp
                ...

                if (loadedStrokes.Count > 0)
                {
                    foreach (Stroke s in loadedStrokes)
                    {
                        _strokeRenderer.Color = s.Color;
                        _strokeRenderer.Ts = s.Ts;
                        _strokeRenderer.Tf = s.Tf;

                        // draw the stroke in the strokes layer
                        _strokeRenderer.ResetAndClear();
                        _strokeRenderer.DrawStroke(s.Path, 0, s.Path.PointsCount, true);
                        _strokeRenderer.BlendStrokeInLayer(_allStrokesLayer, BlendMode.Normal);
                    }

                    // copy the strokes layer to the scene layer
                    _renderingContext.SetTarget(_sceneLayer);
                    _renderingContext.DrawLayer(_allStrokesLayer, null, BlendMode.None);

                    // copy the strokes layer to the backbuffer layer
                    _renderingContext.SetTarget(_backbufferLayer);
                    _renderingContext.DrawLayer(_allStrokesLayer, null, BlendMode.None);

                    // present
                    _graphics.Present();
                }
            }
            catch (System.IO.FileNotFoundException)
            {
            }
        }
```

---

