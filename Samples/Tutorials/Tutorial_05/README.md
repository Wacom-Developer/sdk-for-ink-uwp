# Tutorial 5: Working with Rasters

This tutorial demonstrates how to display raster images with the WILL SDK's drawing engine and how to create raster masks using pointer input.

* [Part 1: Displaying Raster Images](#part-1-displaying-raster-images)
* [Part 2: Image Masking](#part-2-image-masking)

---
## Part 1: Displaying Raster Images

Although WILL SDK is not a general purpose 2D drawing engine, it has the ability to draw raster images. 
This example demonstrates how to load a raster image and display it using functionality provided in the SDK.

Since there are no strokes in this example, we don't have to create a layer for the strokes. 
In the initialization method we create only the backbuffer layer and load the image.

```csharp
        async Task InitInkRendering()
        {
            _graphics.Initialize(this.DxPanel);
            _renderingContext = _graphics.GetRenderingContext();

            Size canvasSize = _graphics.Size;
            float scale = _graphics.Scale;

            _backbufferLayer = _graphics.CreateBackbufferLayer();

            await LoadImage();
        }
```

The image loading is the same as in Tutorial 1, Part 5. 
First we obtain PixelData from a file, then we create a texture from the PixelData object and finally we create a layer from the texture. 
The layer reference is kept in the _imageLayer field.

```csharp
        async Task LoadImage()
        {
            await LoadImageTexture();

            Size size = new Size(_texture.Width, _texture.Height);

            _imageLayer = _graphics.CreateLayer(size, _graphics.Scale, _texture);

            DrawImage();
        }

        async Task LoadImageTexture()
        {
            PixelData pixelData = await GetPixelData(new Uri(@"ms-appx:///Assets/image.jpg"));
            _texture = _graphics.CreateTexture(pixelData);
        }

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

We draw the image using the rendering context's DrawLayer method. 
The _imageLayer is rendered into _backbufferLayer using the specified transformation matrix.

```csharp
        void DrawImage()
        {
            // copy the image layer to the backbuffer layer
            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.ClearColor(_backgroundColor);
            _renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

            // present
            _graphics.Present();
        }
```

---
## Part 2: Image Masking

This example demonstrates how to hide a portion of an image using a mask. 
The mask's outline is specified with a path that is created from pointer input.

The image loading and drawing code is based on Part 1 of this tutorial. 
The input handling and the selection stroke rendering is based on Tutorial 4.

For this example we will use the following layers:

* _backbufferLayer - the backbuffer layer
* _imageLayer - a layer that contains the image (like in Tutorial 5, Part 1)
* _sceneLayer - a layer for the whole scene plus the selecting stroke (like in Tutorial 4)
* _maskLayer - a layer for the image mask

We create the mask layer during initialization, using the size of the canvas.

```csharp
        async Task InitInkRendering()
        {
            _graphics.Initialize(this.DxPanel);
            _renderingContext = _graphics.GetRenderingContext();

            Size canvasSize = _graphics.Size;
            float scale = _graphics.Scale;

            _backbufferLayer = _graphics.CreateBackbufferLayer();
            _sceneLayer = _graphics.CreateLayer(canvasSize, scale);
            _maskLayer = _graphics.CreateLayer(canvasSize, scale);
            _strokeRenderer.Init(_graphics, canvasSize, scale);

            await LoadImage();
        }
```

When we receive a PointerPressed or a PointerMoved event, we need to update the view to reflect the advance of the selecting stroke. 
The update method is slightly different compared to the previous tutorials because of the presence of the image layer. 
Like before, we draw the new part of the current stroke into an intermediate layer of the stroke renderer. 
When recomposing the scene, we have to clear the updated rectangle with the background color and then draw the _imageLayer. 
Then we blend the selector stroke into _sceneLayer. 
Finally, we copy the scene to the backbuffer and present it to the screen.

```csharp
        void DrawImageAndCurrentStroke()
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

            // Recompose the scene within the updated area
            Rect updatedRect = _strokeRenderer.UpdatedRect;
            Point destLocation = new Point(updatedRect.X, updatedRect.Y);

            // Draw background and image
            _renderingContext.SetTarget(_sceneLayer, _strokeRenderer.UpdatedRect);
            _renderingContext.ClearColor(_backgroundColor);
            _renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

            // Draw the new part
            _strokeRenderer.BlendStrokeUpdatedAreaInLayer(_sceneLayer, BlendMode.Normal);

            // copy to backbuffer and present
            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.DrawLayer(_sceneLayer, null, BlendMode.None);
            _graphics.Present();
        }
```

When we receive a PointerReleased event, we create a mask based on the path of the selection stroke. 
To do this we clear the _maskLayer and draw the path into it using the FillPath method of the rendering context. 
We have to draw the _maskLayer after the _imageLayer to achieve the masking effect.

```csharp
        void DrawImage()
        {
            Path currentPath = _pathBuilder.CurrentPath;

            bool drawMask = (currentPath.PointsCount > 0);

            if (drawMask)
            {
                _renderingContext.SetTarget(_maskLayer);
                _renderingContext.ClearColor(_backgroundColor);
                _renderingContext.FillPath(currentPath, Color.FromArgb(255, 255, 255, 255), true);
            }

            // copy the image layer to the backbuffer layer
            _renderingContext.SetTarget(_backbufferLayer);
            _renderingContext.ClearColor(_backgroundColor);
            _renderingContext.DrawLayer(_imageLayer, _imageMatrix, BlendMode.Normal);

            if (drawMask)
            {
                _renderingContext.DrawLayer(_maskLayer, null, BlendMode.MultiplyNoAlpha);
            }

            // present
            _graphics.Present();
        }
```

---
