using Microsoft.Graphics.Canvas;
using Microsoft.Graphics.Canvas.Geometry;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Numerics;
using System.Runtime.InteropServices.WindowsRuntime;
using System.Text;
using System.Threading.Tasks;
using Wacom.Ink.Rendering;
using Windows.Graphics.Imaging;
using Windows.Storage;
using Windows.Storage.Streams;
using Windows.UI;

namespace Wacom
{
    public static class Utils
    {
        /// <summary>
        /// Disposes an object if it is disposable and not null
        /// </summary>
        /// <param name="obj">object to Dispose</param>
        public static void SafeDispose(object obj)
        {
            IDisposable disposable = obj as IDisposable;

            if (disposable != null)
            {
                disposable.Dispose();
            }
        }

    }
}
