# Tutorial 6: Pen Id

This tutorial demonstrates how to obtain a unique identifier for a pen and use it in an application.

* [Part 1: Using Pen Id](#part-1-using-pen-id)

---
## Part 1: Using Pen Id

Pen Id is a 64 bit number that uniquely identifies a pen device. 
Knowing which pen is used for a particular stroke allows for various features to be added to an ink-enabled application. 
For example you can assign different settings for different pen devices, thus letting the users change the style of a stroke just by switching the pen they use.

In this tutorial we will assign a random color to each pen device that is used. 
For this purpose we create a Dictionary that will map pen Ids to colors.

```csharp
    private Dictionary<ulong, Color> _penIdToColorMap = new Dictionary<ulong, Color>();
```

Whenever a new stroke is started we obtain the Id of the pen that is used.

```csharp
        void OnPointerInputBegin(PointerRoutedEventArgs e)
        {
            // If currently there is an unfinished stroke - do not interrupt it
            if (_pointerId.HasValue)
                return;

            ...

            // Get a unique Id for the pen and associate it with a color
            ulong uid = PenIdHelper.GetUniquePenId(e.GetCurrentPoint(this.DxPanel));

            PenIdTextBlock.Text = uid.ToString();
```

We try to get an existing color for this pen Id in the id-to-color map. 
If an entry doesn't exist, we generate a new random color and assign it to the pen. 
Then we set the color as the current color of the stroke renderer.

```csharp
            Color color;
            if (!_penIdToColorMap.TryGetValue(uid, out color))
            {
                color = GetRandomColor();
                _penIdToColorMap[uid] = color;
            }

            // Set a random color for the new stroke
            _strokeRenderer.Color = color;

            // Add the pointer point to the path builder
            AddCurrentPointToPathBuilder(InputPhase.Begin, e);

            // Draw the scene
            Render();
        }
```

Note that the GetUniquePenId method will return 0 in cases when the Id cannot be obtained. 
This can happen if the pen hardware does not provide an Id or if the input comes from another pointer type like mouse or touch. 
In our tutorial the strokes that are produced with mouse or touch will be displayed with one and the same color.

---
