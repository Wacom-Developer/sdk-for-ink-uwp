using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Windows.Devices.Input;
using Windows.Security.ExchangeActiveSyncProvisioning;

using Wacom.Ink;
using Wacom.Ink.Geometry;
using Wacom.Ink.Rendering;
using Wacom.Ink.Serialization;
using Wacom.Ink.Serialization.Model;

// Alias to avoid ambiguity with Wacom.Ink.Serialization.Model.Color
using MediaColor = Windows.UI.Color;

namespace Wacom
{
    public class Serializer 
    {
        #region Fields
        private EasClientDeviceInformation mEAS = new EasClientDeviceInformation();

        private Wacom.Ink.Serialization.Model.Environment mEnvironment = new Wacom.Ink.Serialization.Model.Environment();
        private Dictionary<Identifier, Wacom.Ink.Serialization.Model.Environment> mEnvironments = new Dictionary<Identifier, Wacom.Ink.Serialization.Model.Environment>();

        private Dictionary<Identifier, SensorData> mSensorDataMap = new Dictionary<Identifier, SensorData>(); private Dictionary<PointerDeviceType, Identifier> mDeviceTypeMap = new Dictionary<PointerDeviceType, Identifier>();
        private Dictionary<Identifier, InkInputProvider> mInkInputProvidersMap = new Dictionary<Identifier, InkInputProvider>();
        private Dictionary<Identifier, SensorChannelsContext> mSensorChannelsContexts = new Dictionary<Identifier, SensorChannelsContext>();
        private InputDevice mCurrentInputDevice = new InputDevice();
        private Dictionary<Identifier, InputDevice> mInputDevices = new Dictionary<Identifier, InputDevice>();
        private Dictionary<Identifier, SensorContext> mSensorContexts = new Dictionary<Identifier, SensorContext>();
        private Dictionary<Identifier, InputContext> mInputContexts = new Dictionary<Identifier, InputContext>();

        #endregion

        public InkModel InkDocument { get; set; } 

        public Serializer()
        {
            InitInputConfig();
        }

        public void Init()
        {
            InkDocument = new InkModel();
            InkDocument.InkTree.Root = new StrokeGroupNode(Identifier.FromNewGuid());
        }

        private void InitInputConfig()
        {
            // Init input providers
            var mouseInputProvider = new InkInputProvider(InkInputType.Mouse);
            var touchInputProvider = new InkInputProvider(InkInputType.Touch);
            var penInputProvider = new InkInputProvider(InkInputType.Pen);

            mouseInputProvider.Seal();
            touchInputProvider.Seal();
            penInputProvider.Seal();

            // Cache input providers
            mInkInputProvidersMap.Add(mouseInputProvider.Id, mouseInputProvider);
            mInkInputProvidersMap.Add(touchInputProvider.Id, touchInputProvider);
            mInkInputProvidersMap.Add(penInputProvider.Id, penInputProvider);

            // Init input device
            mCurrentInputDevice.Properties["dev.name"] = mEAS.FriendlyName;
            mCurrentInputDevice.Properties["dev.model"] = mEAS.SystemProductName;
            mCurrentInputDevice.Properties["dev.manufacturer"] = mEAS.SystemManufacturer;
            mCurrentInputDevice.Seal();

            // Cache input device
            mInputDevices.Add(mCurrentInputDevice.Id, mCurrentInputDevice);

            // Init environment
            mEnvironment.Properties["os.name"] = mEAS.OperatingSystem;
            mEnvironment.Properties["os.version.code"] = System.Environment.OSVersion.Version.ToString();
            mEnvironment.Seal();

            // Cache the environment
            mEnvironments.Add(mEnvironment.Id, mEnvironment);

            // Init sensor channels contexts
            SensorChannelsContext mouseSensorChannelsContext = CreateChannelsGroup(mouseInputProvider, mCurrentInputDevice); 
            SensorChannelsContext touchSensorChannelsContext = CreateChannelsGroup(touchInputProvider, mCurrentInputDevice); 
            SensorChannelsContext penSensorChannelsContext = CreateChannelsGroup(penInputProvider, mCurrentInputDevice); 

            // Cache sensor channels contexts
            mSensorChannelsContexts.Add(mouseSensorChannelsContext.Id, mouseSensorChannelsContext);
            mSensorChannelsContexts.Add(touchSensorChannelsContext.Id, touchSensorChannelsContext);
            mSensorChannelsContexts.Add(penSensorChannelsContext.Id, penSensorChannelsContext);

            // Init sensor contexts
            SensorContext mouseSensorContext = new SensorContext();
            mouseSensorContext.AddSensorChannelsContext(mouseSensorChannelsContext);

            SensorContext touchSensorContext = new SensorContext();
            touchSensorContext.AddSensorChannelsContext(touchSensorChannelsContext);

            SensorContext penSensorContext = new SensorContext();
            penSensorContext.AddSensorChannelsContext(penSensorChannelsContext);

            // Cache sensor contexts
            mSensorContexts.Add(mouseSensorContext.Id, mouseSensorContext);
            mSensorContexts.Add(touchSensorContext.Id, touchSensorContext);
            mSensorContexts.Add(penSensorContext.Id, penSensorContext);

            // Init input contexts
            InputContext mouseInputContext = new InputContext(mEnvironment.Id, mouseSensorContext.Id);
            InputContext touchInputContext = new InputContext(mEnvironment.Id, touchSensorContext.Id);
            InputContext penInputContext = new InputContext(mEnvironment.Id, penSensorContext.Id);

            // Cache input contexts
            mInputContexts.Add(mouseInputContext.Id, mouseInputContext);
            mInputContexts.Add(touchInputContext.Id, touchInputContext);
            mInputContexts.Add(penInputContext.Id, penInputContext);

            mDeviceTypeMap.Add(PointerDeviceType.Mouse, mouseInputContext.Id);
            mDeviceTypeMap.Add(PointerDeviceType.Touch, touchInputContext.Id);
            mDeviceTypeMap.Add(PointerDeviceType.Pen, penInputContext.Id);
        }

        public static SensorChannelsContext CreateChannelsGroup(
           InkInputProvider inkInputProvider,
           InputDevice inputDevice,
           uint? latency = null,
           uint? samplingRateHint = null)
        {
            const uint precision = 2;

            List<SensorChannel> sensorChannels = new List<SensorChannel>();
            sensorChannels.Add(new SensorChannel(InkSensorType.X, InkSensorMetricType.Length, 0, 0, 0, precision));
            sensorChannels.Add(new SensorChannel(InkSensorType.Y, InkSensorMetricType.Length, 0, 0, 0, precision));
            sensorChannels.Add(new SensorChannel(InkSensorType.Timestamp, InkSensorMetricType.Time, 0, 0, 0, 0));

            SensorChannelsContext sensorChannelsContext = new SensorChannelsContext(
                inkInputProvider,
                inputDevice,
                sensorChannels,
                latency,
                samplingRateHint);

            return sensorChannelsContext;
        }

        public void EncodeStroke(VectorInkStroke stroke)
        {
            var vectorBrush = new Wacom.Ink.Serialization.Model.VectorBrush(
                                    "will://examples/brushes/" + Guid.NewGuid().ToString(),
                                    stroke.VectorBrush.Polygons);

            var style = new Wacom.Ink.Serialization.Model.Style(vectorBrush.Name);
            style.PathPointProperties.Red = stroke.Color.R / 255.0f;
            style.PathPointProperties.Green = stroke.Color.G / 255.0f;
            style.PathPointProperties.Blue = stroke.Color.B / 255.0f;
            style.PathPointProperties.Alpha = stroke.Color.A / 255.0f;

            AddVectorBrushToInkDoc(stroke.PointerDeviceType.ToString(), vectorBrush, style);
            EncodeStrokeCommon(stroke.Id, stroke.Spline.Clone(), stroke.SensorDataId, style);
        }

        public void EncodeStroke(RasterInkStroke stroke)
        {
            var rasterBrush = stroke.RasterBrush;
            var style = new Wacom.Ink.Serialization.Model.Style(rasterBrush.Name);
            style.PathPointProperties.Red = stroke.StrokeConstants.Color.R / 255.0f;
            style.PathPointProperties.Green = stroke.StrokeConstants.Color.G / 255.0f;
            style.PathPointProperties.Blue = stroke.StrokeConstants.Color.B / 255.0f;
            style.PathPointProperties.Alpha = stroke.StrokeConstants.Color.A / 255.0f;

            AddRasterBrushToInkDoc(stroke.PointerDeviceType, rasterBrush, style, stroke.StrokeConstants, stroke.RandomSeed);
            EncodeStrokeCommon(stroke.Id, stroke.Spline, stroke.SensorDataId, style);
        }

        public Identifier AddSensorData(PointerDeviceType deviceType, List<PointerData> pointerDataList)
        {
            Identifier inputContextId = mDeviceTypeMap[deviceType];
            InputContext inputContext = mInputContexts[inputContextId];
            SensorContext sensorContext = mSensorContexts[inputContext.SensorContextId];

            // Create sensor data using the input context
            SensorData sensorData = new SensorData(
                Identifier.FromNewGuid(),
                inputContext.Id,
                InkState.Plane);

            PopulateSensorData(sensorData, sensorContext, pointerDataList);

            mSensorDataMap.TryAdd(sensorData.Id, sensorData);

            return sensorData.Id;
        }

        private void EncodeStrokeCommon(Identifier id, Spline spline, Identifier sensorDataId, Style style)
        {
            Stroke stroke = new Stroke(
                id,
                spline.Clone(),
                style,
                sensorDataId);

            StrokeNode strokeNode = new StrokeNode(stroke);
            InkDocument.InkTree.Root.Add(strokeNode);

            if (sensorDataId != Identifier.Empty)
            {
                SensorData sensorData = mSensorDataMap[sensorDataId];

                AddSensorDataToModel(sensorData);
            }
        }

        private void AddRasterBrushToInkDoc(PointerDeviceType deviceType, RasterBrush rasterBrush, Style rasterStyle, StrokeConstants strokeConstants, uint startRandomSeed)
        {
            if (!InkDocument.Brushes.TryGetBrush(rasterBrush.Name, out Brush foundBrush))
            {
                InkDocument.Brushes.AddRasterBrush(rasterBrush);
            } 
        }

        private void AddVectorBrushToInkDoc(string pointerDeviceType, Wacom.Ink.Serialization.Model.VectorBrush vectorBrush, Style style)
        {
            if (!InkDocument.Brushes.TryGetBrush(vectorBrush.Name, out Brush foundBrush))
            {
                InkDocument.Brushes.AddVectorBrush(vectorBrush);
            }
        }

        private InkInputProvider CreateAndAddInkProvider(string pointerDeviceType)
        {
            InkInputProvider inkInputProvider = new InkInputProvider((InkInputType)Enum.Parse(typeof(InkInputType), pointerDeviceType));
            //inkInputProvider.AddProperty(); // Add properties if any
            inkInputProvider.Seal();

            Identifier inkInputProviderId = inkInputProvider.Id;
            bool res = InkDocument.InputConfiguration.InkInputProviders.Any((prov) => prov.Id == inkInputProviderId);

            if (!res)
            {
                InkDocument.InputConfiguration.InkInputProviders.Add(inkInputProvider);
            }


            return inkInputProvider;
        }


        private InputDevice CreateAndAddInputDevice()
        {
            InputDevice inputDevice = new InputDevice();
            inputDevice.Properties["dev.name"] = System.Environment.MachineName;
            inputDevice.Properties["dev.model"] = mEAS.SystemProductName;
            inputDevice.Properties["dev.manufacturer"] = mEAS.SystemManufacturer;
            inputDevice.Seal();

            Identifier inputDeviceId = inputDevice.Id;
            bool res = InkDocument.InputConfiguration.Devices.Any((device) => device.Id == inputDeviceId);

            if (!res)
            {
                InkDocument.InputConfiguration.Devices.Add(inputDevice);
            }

            return inputDevice;
        }

        private void PopulateSensorData(SensorData sensorData, SensorContext sensorContext, List<PointerData> pointerDataList)
        {
            SensorChannelsContext channels = sensorContext.DefaultSensorChannelsContext;

            sensorData.AddData(channels.GetChannel(InkSensorType.X), pointerDataList.Select((pd) => pd.X).ToList());
            sensorData.AddData(channels.GetChannel(InkSensorType.Y), pointerDataList.Select((pd) => pd.Y).ToList());
            sensorData.AddTimestampData(channels.GetChannel(InkSensorType.Timestamp), pointerDataList.Select((pd) => pd.Timestamp).ToList());
        }

        private void AddSensorDataToModel(SensorData sensorData)
        {
            // Add sensor data if missing
            if (!InkDocument.SensorData.ContainsId(sensorData.Id))
            {
                InkDocument.SensorData.Add(sensorData);

                InputContext inputContext = mInputContexts[sensorData.InputContextID];

                // Add input context if missing
                if (!InkDocument.InputConfiguration.InputContexts.Contains(inputContext))
                {
                    InkDocument.InputConfiguration.InputContexts.Add(inputContext);

                    Wacom.Ink.Serialization.Model.Environment environment = mEnvironments[inputContext.EnvironmentId];

                    // Add environment if missing
                    if (!InkDocument.InputConfiguration.Environments.Contains(environment))
                    {
                        InkDocument.InputConfiguration.Environments.Add(environment);
                    }

                    SensorContext sensorContext = mSensorContexts[inputContext.SensorContextId];

                    // Add sensor context if missing
                    if (!InkDocument.InputConfiguration.SensorContexts.Contains(sensorContext))
                    {
                        InkDocument.InputConfiguration.SensorContexts.Add(sensorContext);

                        // Iterate and add sensor channels contexts if missing
                        for (int i = 0; i < sensorContext.SensorChannelContexts.Count; i++)
                        {
                            SensorChannelsContext sensorChannelsContext = sensorContext.SensorChannelContexts[i];

                            // Add input device if missing
                            if (!InkDocument.InputConfiguration.Devices.Contains(sensorChannelsContext.InputDevice))
                            {
                                InkDocument.InputConfiguration.Devices.Add(sensorChannelsContext.InputDevice);
                            }

                            // Add ink input provider if missing
                            if (!InkDocument.InputConfiguration.InkInputProviders.Contains(sensorChannelsContext.InkInputProvider))
                            {
                                InkDocument.InputConfiguration.InkInputProviders.Add(sensorChannelsContext.InkInputProvider);
                            }
                        }
                    }
                }
            }
        }

        public void LoadSensorDataFromModel(InkModel inkModel, SensorData sensorData)
        {
            // Load sensor data if missing
            if (mSensorDataMap.TryAdd(sensorData.Id, sensorData))
            {
                InputContext inputContext = inkModel.InputConfiguration.InputContexts.Find((ic) => ic.Id == sensorData.InputContextID);

                // Load input context if missing
                if (mInputContexts.TryAdd(inputContext.Id, inputContext))
                {
                    Wacom.Ink.Serialization.Model.Environment environment = inkModel.InputConfiguration.Environments.Find((env) => env.Id == inputContext.EnvironmentId);
                    mEnvironments.TryAdd(environment.Id, environment);

                    SensorContext sensorContext = inkModel.InputConfiguration.SensorContexts.Find((sc) => sc.Id == inputContext.SensorContextId);

                    // Load sensor context if missing
                    if (mSensorContexts.TryAdd(sensorContext.Id, sensorContext))
                    {
                        // Iterate and load sensor channels contexts if missing
                        for (int j = 0; j < sensorContext.SensorChannelContexts.Count; j++)
                        {
                            SensorChannelsContext sensorChannelsContext = sensorContext.SensorChannelContexts[j];

                            // Load input device if missing
                            mInputDevices.TryAdd(sensorChannelsContext.InputDevice.Id, sensorChannelsContext.InputDevice);

                            // Load ink input provider if missing
                            mInkInputProvidersMap.TryAdd(sensorChannelsContext.InkInputProvider.Id, sensorChannelsContext.InkInputProvider);
                        }
                    }
                }
            }
        }

    }
}
