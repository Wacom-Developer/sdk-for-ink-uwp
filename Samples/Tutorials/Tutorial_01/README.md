# Tutorial 1: Drawing with Pointing Devices

This tutorial demonstrates how to draw strokes with WILL SDK using input data from pointing devices.

It covers basic topics like initialization of the WILL SDK's ink engine, drawing of strokes, input handling, smoothing and preliminary paths.

The tutorial contains the following parts:

* [Part 1: Ink Engine Setup](#part-1-ink-engine-setup)
* [Part 2: Building Paths from Pointer Input](#part-2-building-paths-from-pointer-input)
* [Part 3: Smoothing](#part-3-smoothing)
* [Part 4: Preliminary Path](#part-4-preliminary-path)
* [Part 5: Particle Brush](#part-5-particle-brush)
* [Part 6: Translucent Solid Color Brush](#part-6-translucent-solid-color-brush)

---
## Part 1: Ink Engine Setup

The WILL SDK's drawing engine for Windows is based on DirectX 11 and can be integrated with a Windows Store application with the help of a SwapChainPanel. 
To add a SwapChainPanel object to a page you have to place a SwapChainPanel element in the page's XAML:

```csharp
<Grid>
    ...

    <SwapChainPanel x:Name="DxPanel" HorizontalAlignment="Stretch" VerticalAlignment="Stretch" />

    ...
</Grid>
```
A SwapChainPanel instance called DxPanel now exists in the page and we have to associate it with WILL SDK's drawing engine. 
For this purpose we create an object of type Graphics (Wacom.Ink.Graphics) and call its Initialize method, passing the DxPanel as a parameter. 
This must happen after the DxPanel's ActualWidth and ActualHeight are calculated, for example in the handler of the Page_Loaded event. 
Note that it is not appropriate to call Initialize in the page's constructor.

```csharp
    void Page_Loaded(object sender, RoutedEventArgs e)
    {
        // Create an inking graphics object and associate it with the DX panel
        _graphics = new Graphics();
        _graphics.Initialize(this.DxPanel);
        ...
```

The purpose of a graphics object is to create resources (layers and textures) and to manage the DirectX swap chain. 
The pure rendering functionality is implemented by another type, called RenderingContext. 
The Graphics object has an associated rendering context and we can obtain a reference to it using the Graphics object's GetRenderingContext method:

```csharp
        // Obtain the rendering context
        _renderingContext = _graphics.GetRenderingContext();
```

Layer objects represent surfaces that act as targets for rendering. 
For the current example we can use only one layer and it should be the layer that is associated with the DirectX backbuffer. 
We can use the following code to obtain a reference to this layer:

```csharp
        // Create a layer associated with the DirectX backbuffer
        _backbufferLayer = _graphics.CreateBackbufferLayer();
```

The StrokeRenderer type provides a simple and efficient mechanism for rendering of strokes. 
The following code creates and configures an instance of the StrokeRenderer class.

```csharp
        // Create a stroke renderer
        _strokeRenderer = new StrokeRenderer();
        _strokeRenderer.Init(_graphics, _graphics.Size, _graphics.Scale);
        _strokeRenderer.Brush = new SolidColorBrush();
        _strokeRenderer.StrokeWidth = 4;
        _strokeRenderer.Color = Colors.Red;
        _strokeRenderer.UseVariableAlpha = false;
        _strokeRenderer.Ts = 0.0f;
        _strokeRenderer.Tf = 1.0f;
```

At this point we are ready to start drawing. 
Our goal in this part of the tutorial will be just to clear the screen and draw a static stroke.

The stroke's path is specified as an array of control points. 
Each point has a number of attributes like X position, Y position, Width and Alpha value. 
The X / Y coordinates are mandatory, while the others are optional. 
All the points passed to a particular DrawStroke call should have the same number of attributes. 
For example you cannot mix (X, Y) points with (X, Y, Width) points in the same DrawStroke call.

The control points are stored as a list of float values (IList). 
The attributes of each point are placed in sucessive order in the list, and the same order must be maintained for all points. 
In the example below we define 4 control points with their X and Y coordinates: {X:100, Y:120}, {X:200, Y:210}, {X:400, Y:120}, {X:500, Y:400}. 
The points array is used to create a Path object.

```csharp
            float[] points = new float[] { 100, 120, 200, 210, 400, 120, 500, 400 };

            Path path = new Path(points, 2);
```

Note that in the Path constructor we set the stride parameter to 2. 
The stride is the step between the starting indices of two successive points. 
In other words, the stride is the same as the number of point attributes (in this case X and Y).

The path that we defined doesn't have individual Width values for the points. 
An uniform width of 4 will be used for the whole stroke - this was specified earlier throught the StrokeWidth property of the stroke renderer.

To render the stroke we use the DrawStroke method of the stroke renderer. 
The stroke is rendered in an intermediate layer. 
The ResetAndClear method should be called every time before a new stroke is started to clear the intermediate layer and reset the object's state.

```csharp
            _strokeRenderer.ResetAndClear();
            _strokeRenderer.DrawStroke(path, 0, path.PointsCount, true);
```

We use the SetTarget method to instruct the rendering context to use _backbufferLayer as a rendering target. 
The the ClearColor method fills the whole layer with the specified solid color.

```csharp
            // Clear the backbuffer layer
            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.ClearColor(_backgroundColor);
```

Now we have to blend the stroke from the stroke renderer to the backbuffer and then present the backbuffer to the screen.

```csharp
            _strokeRenderer.BlendStrokeInLayer(_backbufferLayer, BlendMode.Normal);

            // Present the backbuffer
            _graphics.Present();
```

When you run the example you will notice that the first point and the last point are not present in the curve and only one spline segment is drawn (between the second and the third point). 
However, if you change the coordinates of the endpoints, you will notice that they affect the curve. 
This behavior is normal and is due to the nature of the Catmull-Rom splines that are used internally.

Above we presented the minimum amount of code that is necessary to display a stroke with WILL SDK. 
In a real life application we will need some additional code to handle events that are triggered by the environment.

For example when the DxPanel is resized we have to recreate the view layer and reinitialize the stroke renderer with the new size of the panel. 
We also pass the new size to the graphics object through the SetLogicalSize method.

```csharp
        void DxPanel_SizeChanged(object sender, SizeChangedEventArgs e)
        {
            if (_renderingContext == null)
                return;

            // Release existing layers
            _renderingContext.SetTarget(null);
            _strokeRenderer.Deinit();
            _backbufferLayer.Dispose();

            // Set the new size
            _graphics.SetLogicalSize(e.NewSize);

            // Recreate the layers
            Size canvasSize = _graphics.Size;
            float scale = _graphics.Scale;

            _backbufferLayer = _graphics.CreateBackbufferLayer();

            _strokeRenderer.Init(_graphics, _graphics.Size, _graphics.Scale);

            ...
        }
```

When the app is being suspended, the Trim method of the graphics should be called in order to free graphics memory allocated on the app's behalf.

```csharp
public Part1()
        {
            ...

            Application.Current.Suspending += App_Suspending;

            ...
        }

        void App_Suspending(object sender, Windows.ApplicationModel.SuspendingEventArgs e)
        {
            if (_graphics != null)
            {
                _graphics.Trim();
            }
        }
```

For more information on the Trim method you can visit: IDXGIDevice3::Trim.

Another event that should be handled is DisplayInformation.DisplayContentsInvalidated. 
It occurs when the display requires redrawing and we have to call the ValidateDevice in response.

```csharp
        public Part1()
        {
            ...

            DisplayInformation.DisplayContentsInvalidated += DisplayInformation_DisplayContentsInvalidated;

            ...
        }

        void DisplayInformation_DisplayContentsInvalidated(DisplayInformation sender, object args)
        {
            if (_graphics != null)
            {
                _graphics.ValidateDevice();
            }
        }
```

For more information on this event, please see: *DisplayInformation.DisplayContentsInvalidated*.

Note that the objects from WILL SDK implement IDisposable and you have to dispose them when they are no longer necessary.

---
## Part 2: Building Paths from Pointer Input

This part demonstrates how to draw strokes interactively while receiving input points from mouse, touch or pen/stylus.

It is assumed that you have basic knowledge of Windows Runtime user interactions and in particular pointer input. 
The following MSDN article may help you get acquainted with the topic:

http://msdn.microsoft.com/en-us/library/windows/apps/xaml/jj150606.aspx

We already know how to draw a stroke using a predefined path of control points. 
Now we need to build the path "on the fly" from points that are supplied in the pointer events. 
For this purpose we create a path builder object (in this case a SpeedPathBuilder) and configure it to produce paths with variable width depending on the pointer's velocity. 
We also create a StrokeRenderer and set its StrokeWidth property to null, which means that it should use the variable width provided by the path.

```csharp
        void CreateWacomInkObjects()
        {
            // Create a graphics object
            _graphics = new Graphics();

            // Create a path builder
            _pathBuilder = new SpeedPathBuilder();
            _pathBuilder.SetMovementThreshold(0.1f);
            _pathBuilder.SetNormalizationConfig(100.0f, 4000.0f);
            _pathBuilder.SetPropertyConfig(PropertyName.Width, 2.0f, 30.0f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);

            // Create a stroke renderer
            _strokeRenderer = new StrokeRenderer();
            _strokeRenderer.Brush = new SolidColorBrush();
            _strokeRenderer.StrokeWidth = null;
            _strokeRenderer.Color = Colors.Crimson;
            _strokeRenderer.UseVariableAlpha = false;
            _strokeRenderer.Ts = 0.0f;
            _strokeRenderer.Tf = 1.0f;
        }
```

The rendering initialization is placed in the event handler of the Page's Loaded event and is similar to the previous part of the tutorial. 
The notable difference is that now we create a second layer (_strokesLayer) that will store the output for the current stroke. 
Whenever new input points arrive, we will draw the new portions of the stroke in this layer. 

```csharp
The _strokesLayer will be cleared only when a new stroke begins.
        void Page_Loaded(object sender, RoutedEventArgs e)
        {
            InitInkRendering();

            ...
        }

        void InitInkRendering()
        {
            _graphics.Initialize(this.DxPanel);
            _renderingContext = _graphics.GetRenderingContext();

            InitSizeDependentResources();
        }

        void InitSizeDependentResources()
        {
            Size canvasSize = _graphics.Size;
            float scale = _graphics.Scale;

            _backbufferLayer = _graphics.CreateBackbufferLayer();
            _strokeLayer = _graphics.CreateLayer(canvasSize, scale);
            _strokeRenderer.Init(_graphics, canvasSize, scale);

            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.ClearColor(_backgroundColor);
            _graphics.Present();
        }
```

Handling of pointer events can be implemented in various ways, depending on the particular requirements of the application. 
In our case we use a basic implementation that receives input from a single pointer and builds a path using the input points. 
The same approach is used in all tutorials, regardless of whether the path is rendered as a stroke or used for manipulations.

We will handle the following pointer events:

```csharp
        void Page_Loaded(object sender, RoutedEventArgs e)
        {
            ...

            // Attach to swap chain panel events
            this.DxPanel.PointerPressed += DxPanel_PointerPressed;
            this.DxPanel.PointerMoved += DxPanel_PointerMoved;
            this.DxPanel.PointerReleased += DxPanel_PointerReleased;
            this.DxPanel.PointerCaptureLost += DxPanel_PointerCaptureLost;
            this.DxPanel.PointerCanceled += DxPanel_PointerCanceled;

            ...
        }

        void DxPanel_PointerPressed(object sender, PointerRoutedEventArgs e)
        {
            OnPointerInputBegin(e);
        }

        void DxPanel_PointerMoved(object sender, PointerRoutedEventArgs e)
        {
            OnPointerInputMove(e);
        }

        void DxPanel_PointerReleased(object sender, PointerRoutedEventArgs e)
        {
            OnPointerInputEnd(e);
        }

        void DxPanel_PointerCanceled(object sender, PointerRoutedEventArgs e)
        {
            OnPointerInputEnd(e);
        }

        void DxPanel_PointerCaptureLost(object sender, PointerRoutedEventArgs e)
        {
            OnPointerInputEnd(e);
        }
```
Whenever a new stroke begins we store the pressed pointer's Id in a nullable field called _pointerId. 
This variable helps us filter out unwanted pointer events. 
There are two state variables that are related to path building: _updateFromIndex and _pathFinished. 
Both of them are reset to their initial values.

For this tutorial we want to see just the current stroke, so we clear the _strokesLayer in OnPointerInputBegin. 
We add the current point to the path with the AddCurrentPointToPathBuilder method and then call Render to update the display.

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

            // Reset the stroke renderer
            _strokeRenderer.ResetAndClear();

            // Clear the scene
            _renderingContext.SetTarget(_strokeLayer);
            _renderingContext.ClearColor(_backgroundColor);

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.Begin, e);

            // Draw the scene
            Render();
        }
```

The AddCurrentPointToPathBuilder method is the place where path building happens. 
For each new point we first call the path builder's AddPoint method which returns a path part and then we call AddPathPart, passing the path part returned by AddPoint. 
These two steps allow for additional processing to occur in between. 
Note that AddPoint needs to know the current phase of the path building. 
You should pass InputPhase.Begin when the stroke starts, then keep passing InputPhase.Move while the stroke continues and finally pass InputPhase.End when the stroke ends.

```csharp
        void AddCurrentPointToPathBuilder(InputPhase phase, PointerRoutedEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(this.DxPanel);

            Path pathPart = _pathBuilder.AddPoint(phase, pointerPoint);

            if (pathPart.PointsCount > 0)
            {
                int indexOfFirstAffectedPoint;
                _pathBuilder.AddPathPart(pathPart, out indexOfFirstAffectedPoint);

                if (_updateFromIndex == -1)
                {
                    _updateFromIndex = indexOfFirstAffectedPoint;
                }
            }
        }
```

The AddPathPart method adds the new path part to the current path. 
Each time we need to Draw only the latest part of the path. 
For this purpose in the _updateFromIndex field we store the index from where we should start to draw. 
After we Draw the new portion of the path (in the Render method), we will set _updateFromIndex to -1, meaning that the path is fully updated and no part of it should be rendered.

The handling of the pointer's move event is much simpler. 
First we check whether the event is from the pointer that we are following, then we add the current point to the path and render the scene.

```csharp
        void OnPointerInputMove(PointerRoutedEventArgs e)
        {
            // Ignore events from other pointers
            if (!_pointerId.HasValue || (e.Pointer.PointerId != _pointerId.Value))
                return;

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.Move, e);

            // Draw the scene
            Render();
        }
```

When the pointer is released or cancelled, we set the _pointerId to null and release the capture. 
We add the final point to the path, set the _pathFinished flag to true and call Render to update the display.

```csharp
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

            _pathFinished = true;

            // Draw the scene
            Render();
        }
```

In the Render method every time we need to draw only the last few path points that have been affected by the path building process - this is the portion from _updateFromIndex to the end of the path. 
We call the DrawStroke method specifying _updateFromIndex as the index of the first point to be rendered. 
After that we reset the _updateFromIndex field to -1 to indicate that all available path points have been rendered.

```csharp
        void Render()
        {
            if (_updateFromIndex < 0)
                return;

            Path currentPath = _pathBuilder.CurrentPath;

            int numberOfPointsToDraw = currentPath.PointsCount - _updateFromIndex;
            if (numberOfPointsToDraw <= 0)
                return;

            _strokeRenderer.DrawStroke(currentPath, _updateFromIndex, numberOfPointsToDraw, _pathFinished);

            _updateFromIndex = -1;

            ...
```
At this point the stroke is rendered in an intermediate layer of the stroke renderer and we have to blend it to the _strokeLayer. 
For performance reasons we don't want to blend the whole stroke, but only the recently updated part of it. 
For this purpose we first have to recompose the scene behind the bounding rectangle of the recent path part - in our case this is as simple as clearing the rectangle with the background color. 
Then we use BlendStrokeUpdatedAreaInLayer to blend the latest portion of the stroke to the _strokesLayer.

Finally the whole _strokesLayer is copied to _backbufferLayer and the scene is presented to the screen.

```csharp
            ...

            // recompose the scene within the updated area
            _renderingContext.SetTarget(_strokeLayer, _strokeRenderer.UpdatedRect);
            _renderingContext.ClearColor(_backgroundColor);

            // draw the new stroke part
            _strokeRenderer.BlendStrokeUpdatedAreaInLayer(_strokeLayer, BlendMode.Normal);

            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.DrawLayer(_strokeLayer, null, BlendMode.None);

            // present
            _graphics.Present();
        }
```
---
# Part 3: Smoothing
You might have noticed that the strokes drawn in the previous part are looking somewhat jagged. 
This effect is caused by the uneven input data that comes from the pointers. 
It is more noticeable with some input devices and less noticeable with others, but in all cases is undesired.

WILL SDK provides a way to make the strokes look smoother by applying a smoothing filter to the points. 
This functionality is implemented by the MultiChannelSmoothener class. 
As its name suggests, it can smooth multiple channels of data, which means that it can work directly with the path parts that are returned by the path builder. 
The attributes of the control points (X, Y, Width, Alpha) are treated as separate channels and are processed independently from each other.

Normally we create a MultiChannelSmoothener instance after the path builder is created and configured. 
Thus we can obtain the stride from the path builder and pass it to the MultiChannelSmoothener's constructor as number of channels.

```csharp
        void CreateWacomInkObjects()
        {
            ...

            // Create a path builder
            _pathBuilder = new SpeedPathBuilder();
            _pathBuilder.SetMovementThreshold(0.1f);
            _pathBuilder.SetNormalizationConfig(100.0f, 4000.0f);
            _pathBuilder.SetPropertyConfig(PropertyName.Width, 2.0f, 30.0f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);

            // Create an object that smooths input data
            _smoothener = new MultiChannelSmoothener(_pathBuilder.PathStride);

            ...
        }
```

Two more steps remain to complete the integration of the smoothener with the existing code.

First we have to reset the smoothener each time when a new stroke is started:

```csharp
        void OnPointerInputBegin(PointerRoutedEventArgs e)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (_pointerId.HasValue)
                return;

            ...

            // Reset the smoother
            _smoothener.Reset();

            ...
        }
```

The second step is to call the Smooth method to process the path parts that are returned from the _pathBuilder's AddPoint method. 
After that the smoothed parts are passed to AddPathPart. 
Note that the Smooth methods's second parameter is a boolean value that specifies whether the path should be finished. 
This parameter must be set to true only for the last part of a path.

```csharp
        void AddCurrentPointToPathBuilder(InputPhase phase, PointerRoutedEventArgs e)
        {
            PointerPoint pointerPoint = e.GetCurrentPoint(this.DxPanel);

            Path pathPart = _pathBuilder.AddPoint(phase, pointerPoint);

            if (pathPart.PointsCount > 0)
            {
                _smoothener.Smooth(pathPart, phase == InputPhase.End);

                int indexOfFirstAffectedPoint;
                _pathBuilder.AddPathPart(pathPart, out indexOfFirstAffectedPoint);

                if (_updateFromIndex == -1)
                {
                    _updateFromIndex = indexOfFirstAffectedPoint;
                }
            }
        }
```

At this point the strokes should look better, but you might notice that they are now lagging behind the pointer. 
This is an inevitable result of the smoothing process, but the next part will explain how to work around this issue.

---
## Part 4: Preliminary Path
As we saw in the previous part, smoothing causes the stroke to lag behind the pointer. 
We can work around this issue by drawing a curve from the last path point to the pointer's current location. 
We call this curve "preliminary path". 
It acts as a kind of prediction for the trajectory of the real path and might prove wrong, especially if the stroke makes a sharp turn. 
We have to make sure that the preliminary path is displayed only temporarily and is replaced by the real path once it is available.

To integrate preliminary path rendering in our example, we have to extend the Render method from Part 3. 
We will insert the new piece of code after the call to DrawStroke. 
We use the pathbuilder's CreatePreliminaryPath method to create a preliminary path. 
After that we smooth it with the Smooth method of the multi-channel smoothener. 
It is important that Smooth is called with its finish parameter set to true to indicate that the smoothing should be finished. 
We pass the smoothed part to FinishPreliminaryPath to make it ready for rendering. 
We call the _strokeRenderer's DrawPreliminaryStroke to draw the preliminary path in an intermediate layer.

```csharp
        void Render(bool drawPreliminaryPath)
        {
            if (_updateFromIndex < 0)
                return;

            Path currentPath = _pathBuilder.CurrentPath;

            int numberOfPointsToDraw = currentPath.PointsCount - _updateFromIndex;
            if (numberOfPointsToDraw <= 0)
                return;

            _strokeRenderer.DrawStroke(currentPath, _updateFromIndex, numberOfPointsToDraw, _pathFinished);

            _updateFromIndex = -1;

            // draw preliminary path
            if (drawPreliminaryPath && !_pathFinished)
            {
                Path prelimPathPart = _pathBuilder.CreatePreliminaryPath();

                if (prelimPathPart.PointsCount > 0)
                {
                    _smoothener.Smooth(prelimPathPart, true);

                    Path preliminaryPath = _pathBuilder.FinishPreliminaryPath(prelimPathPart);

                    _strokeRenderer.DrawPreliminaryStroke(preliminaryPath, 0, preliminaryPath.PointsCount);
                }
            }

            // recompose the scene within the updated area
            _renderingContext.SetTarget(_strokeLayer, _strokeRenderer.UpdatedRect);
            _renderingContext.ClearColor(_backgroundColor);

            // draw
            _strokeRenderer.BlendStrokeUpdatedAreaInLayer(_strokeLayer, BlendMode.Normal);

            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.DrawLayer(_strokeLayer, null, BlendMode.None);

            // present
            _graphics.Present();
        }
```

The rest of the Render method remains the same as in the previous tutorial. 
The BlendStrokeUpdatedAreaInLayer blends the recent stroke part to the _strokeLayer, but now includes the preliminary stroke.

---
# Part 5: Particle Brush

In the previous examples we have used a brush that fills the stroke with a solid color. 
WILL SDK provides another type of brush, called particle brush, that uses textures to produce various visual effects and add some artistic touch to the strokes. 
Basically particle brushes draw large number of small textures scattered along the stroke's trajectory. 
Note that drawing with this kind of brush comes at a certain cost, because it is more computationally expensive.

We have to make a few changes in our code in order to start using a particle brush. 
First of all we have to replace the SolidColorBrush object with an instance of the ParticleBrush type.

```csharp
        void CreateWacomInkObjects()
        {
            // Create a graphics object
            _graphics = new Graphics();

            // Create a path builder
            _pathBuilder = new PressurePathBuilder();
            _pathBuilder.SetMovementThreshold(0.1f);
            _pathBuilder.SetNormalizationConfig(0.0f, 1.0f);
            _pathBuilder.SetPropertyConfig(PropertyName.Width, 10.0f, 40.0f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);
            _pathBuilder.SetPropertyConfig(PropertyName.Alpha, 0.08f, 0.9f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);

            // Create an object that smooths input data
            _smoothener = new MultiChannelSmoothener(_pathBuilder.PathStride);

            // Create a particle brush
            _brush = new ParticleBrush();
            _brush.BlendMode = BlendMode.Normal;
            _brush.Spacing = 0.15f;
            _brush.Scattering = 0.05f;
            _brush.RotationMode = ParticleRotationMode.RotateRandom;
            _brush.FillTileSize = new Size(128.0f, 128.0f);

            // Create a stroke renderer
            _strokeRenderer = new StrokeRenderer();
            _strokeRenderer.Brush = _brush;
            _strokeRenderer.StrokeWidth = null;
            _strokeRenderer.Color = Colors.DarkOrange;
            _strokeRenderer.UseVariableAlpha = true;
            _strokeRenderer.Ts = 0.0f;
            _strokeRenderer.Tf = 1.0f;
        }
```

The particle brush provides various settings that control the appearance of the stroke. 
In particular in this example we set the spacing between the images to be 15% (0.15) of their width, and specify that they will spread out randomly a bit (Scattering = 0.05). 
Random rotation of the images is also enabled.

Note that we have changed the type of the path builder to PressurePathBuilder. 
It will calculate path attributes like width and alpha based on the amount of pressure applied to the input device.

Note that we have added a new setting to the configuration of the path builder: 

```csharp
            _pathBuilder.SetPropertyConfig(PropertyName.Alpha, 0.08f, 0.9f, null, null, PropertyFunction.Sigmoid, 0.6191646f, false);
```

This code makes the transparency of the stroke dependent on the pointer's pressure. 
It is not mandatory to enable this feature when using a particle brush.

After the renderer is initialized we have to assign shape and fill textures to the brush. 
This brush will draw a large number of small images (shape.png) along the path. 
The stroke will be filled by tiling the image in fill.png.

```csharp
        async Task LoadBrushTextures()
        {
            PixelData fillPixelData = await GetPixelData(new Uri(@"ms-appx:///Assets/fill.png"));
            _brush.FillTexture = _graphics.CreateTexture(fillPixelData);

            PixelData shapePixelData = await GetPixelData(new Uri("ms-appx:///Assets/shape.png"));
            _brush.ShapeTexture = _graphics.CreateTexture(shapePixelData);
        }
```

The GetPixelData method used above is a utility that loads an image from a resource into a PixelData object. 
The graphics object has the ability to create textures from PixelData objects.

```csharp
        static async Task<PixelData> GetPixelData(Uri uri)
        {
            StorageFile file = await StorageFile.GetFileFromApplicationUriAsync(uri);

            using (IRandomAccessStream fileStream = await file.OpenAsync(FileAccessMode.Read))
            {
                BitmapDecoder decoder = await BitmapDecoder.CreateAsync(fileStream);

                PixelDataProvider provider = await decoder.GetPixelDataAsync(
                    BitmapPixelFormat.Bgra8,
                    BitmapAlphaMode.Premultiplied,
                    new BitmapTransform(),
                    ExifOrientationMode.IgnoreExifOrientation,
                    ColorManagementMode.DoNotColorManage);

                var buffer = provider.DetachPixelData().AsBuffer();

                return new PixelData(buffer, decoder.PixelWidth, decoder.PixelHeight);
            }
        }
```

The code that renders the stroke remains the same as in the previous part of the tutorial.

---
# Part 6: Translucent Solid Color Brush

The solid color brush does not support variable alpha values, but can be used to translucent strokes with an uniform alpha value.

In order to demonstrate this feature properly we have to draw multiple strokes and leave them on the screen. 
This way the effect of transluceny will be visible when the new strokes are overlapping the previous ones.

The code in this tutorial is based on the code from Part 4. 
Some changes are necessary in order to implement the new scenario.

We want to have multiple strokes on the screen at the same time. 
Instead of a layer that keeps the current stroke we will use two layers: one that will store the whole scene (_sceneLayer), and another one that will store all the strokes except the current one (_allStrokesLayer).

```csharp
        private Layer _sceneLayer;
        private Layer _allStrokesLayer;
```

Each stroke will have a different color, so there is no need to set the _strokeRenderer's Color property in advance. Following is the stroke renderer's configuration:

```csharp
            // Create a stroke renderer
            _strokeRenderer = new StrokeRenderer();
            _strokeRenderer.Brush = new SolidColorBrush();
            _strokeRenderer.StrokeWidth = null;
            _strokeRenderer.UseVariableAlpha = false;
            _strokeRenderer.Ts = 0.0f;
            _strokeRenderer.Tf = 1.0f;
```

The initialization code has not changed much, we initialize the stroke renderer, create the necessary layers and clear them.

```csharp
        void InitSizeDependentResources()
        {
            Size canvasSize = _graphics.Size;
            float scale = _graphics.Scale;

            _backbufferLayer = _graphics.CreateBackbufferLayer();
            _sceneLayer = _graphics.CreateLayer(canvasSize, scale);
            _allStrokesLayer = _graphics.CreateLayer(canvasSize, scale);
            _strokeRenderer.Init(_graphics, canvasSize, scale);

            _renderingContext.SetTarget(_allStrokesLayer);
            _renderingContext.ClearColor(_backgroundColor);

            _renderingContext.SetTarget(_sceneLayer);
            _renderingContext.ClearColor(_backgroundColor);

            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.ClearColor(_backgroundColor);
            _graphics.Present();
        }
```

When a new stroke starts from user input, we set a new random color to the stroke renderer. 
Note that the _allStrokesLayer and the _sceneLayer are not cleared in OnPointerInputBegin, because we want the previous strokes to remain on the screen.

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

            // Reset the stroke renderer
            _strokeRenderer.ResetAndClear();

            // Set a random color for the new stroke
            _strokeRenderer.Color = GetRandomColor();

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.Begin, e);

            // Draw the scene
            Render();
        }
```

When the stroke is finished, we blend the rendered stroke from the stroke renderer to the _allStrokesLayer and we add the corresponding stroke object to the list of strokes.

```csharp
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

            _pathFinished = true;

            // Draw the scene
            Render();

            // Blend the current stroke into the current stroke layer
            _strokeRenderer.BlendStrokeInLayer(_allStrokesLayer, BlendMode.Normal);
        }
```

The rendering has to be changed to support multiple strokes. 
In order to recompose the scene within the updated rectangle we need to copy the rectangle from the _allStrokesLayer.

```csharp
        void Render()
        {
            if (_updateFromIndex < 0)
                return;

            Path currentPath = _pathBuilder.CurrentPath;

            int numberOfPointsToDraw = currentPath.PointsCount - _updateFromIndex;
            if (numberOfPointsToDraw <= 0)
                return;

            _strokeRenderer.DrawStroke(currentPath, _updateFromIndex, numberOfPointsToDraw, _pathFinished);

            // reset the starting index
            _updateFromIndex = -1;

            // draw preliminary path
            if (!_pathFinished)
            {
                Path prelimPathPart = _pathBuilder.CreatePreliminaryPath();

                if (prelimPathPart.PointsCount > 0)
                {
                    _smoothener.Smooth(prelimPathPart, true);

                    Path preliminaryPath = _pathBuilder.FinishPreliminaryPath(prelimPathPart);

                    _strokeRenderer.DrawPreliminaryStroke(preliminaryPath, 0, preliminaryPath.PointsCount);
                }
            }

            // recompose the scene within the updated area
            Rect updatedRect = _strokeRenderer.UpdatedRect;
            Point destLocation = new Point(updatedRect.X, updatedRect.Y);

            // draw background and previous strokes
            _renderingContext.SetTarget(_sceneLayer);
            _renderingContext.DrawLayer(_allStrokesLayer, updatedRect, destLocation, BlendMode.None);

            // draw the new part
            _strokeRenderer.BlendStrokeUpdatedAreaInLayer(_sceneLayer, BlendMode.Normal);

            // copy to backbuffer and present
            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.DrawLayer(_sceneLayer, null, BlendMode.None);
            _graphics.Present();
        }
```

---
