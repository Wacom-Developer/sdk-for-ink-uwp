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
        private EasClientDeviceInformation m_eas = new EasClientDeviceInformation();

        private Wacom.Ink.Serialization.Model.Environment mEnvironment = new Wacom.Ink.Serialization.Model.Environment();
        private Dictionary<Identifier, Wacom.Ink.Serialization.Model.Environment> m_environments = new Dictionary<Identifier, Wacom.Ink.Serialization.Model.Environment>();
        private readonly SensorChannel mTimestampSensorChannel;

        private Dictionary<Identifier, SensorData> m_sensorDataMap = new Dictionary<Identifier, SensorData>(); private Dictionary<PointerDeviceType, Identifier> m_deviceTypeMap = new Dictionary<PointerDeviceType, Identifier>();
        private Dictionary<Identifier, InkInputProvider> m_inkInputProvidersMap = new Dictionary<Identifier, InkInputProvider>();
        private Dictionary<Identifier, SensorChannelsContext> m_sensorChannelsContexts = new Dictionary<Identifier, SensorChannelsContext>();
        private InputDevice m_currentInputDevice = new InputDevice();
        private Dictionary<Identifier, InputDevice> m_inputDevices = new Dictionary<Identifier, InputDevice>();
        private Dictionary<Identifier, SensorContext> m_sensorContexts = new Dictionary<Identifier, SensorContext>();
        private Dictionary<Identifier, InputContext> m_inputContexts = new Dictionary<Identifier, InputContext>();

        #endregion

        public InkModel InkDocument { get; set; } 

        public Serializer()
        {
            mTimestampSensorChannel = new SensorChannel(
                InkSensorType.Timestamp,
                InkSensorMetricType.Time,
                null, 0.0f, 0.0f, 0);

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
            m_inkInputProvidersMap.Add(mouseInputProvider.Id, mouseInputProvider);
            m_inkInputProvidersMap.Add(touchInputProvider.Id, touchInputProvider);
            m_inkInputProvidersMap.Add(penInputProvider.Id, penInputProvider);

            // Init input device
            m_currentInputDevice.Properties["dev.name"] = m_eas.FriendlyName;
            m_currentInputDevice.Properties["dev.model"] = m_eas.SystemProductName;
            m_currentInputDevice.Properties["dev.manufacturer"] = m_eas.SystemManufacturer;
            m_currentInputDevice.Seal();

            // Cache input device
            m_inputDevices.Add(m_currentInputDevice.Id, m_currentInputDevice);

            // Init environment
            mEnvironment.Properties["os.name"] = m_eas.OperatingSystem;
            mEnvironment.Properties["os.version.code"] = System.Environment.OSVersion.Version.ToString();
            mEnvironment.Seal();

            // Cache the environment
            m_environments.Add(mEnvironment.Id, mEnvironment);

            // Init sensor channels contexts
            SensorChannelsContext mouseSensorChannelsContext = SensorChannelsContext.CreateDefault(mouseInputProvider, m_currentInputDevice); //new SensorChannelsContext(mouseInputProvider, m_currentInputDevice, m_sensorChannels);
            SensorChannelsContext touchSensorChannelsContext = SensorChannelsContext.CreateDefault(touchInputProvider, m_currentInputDevice); //new SensorChannelsContext(touchInputProvider, m_currentInputDevice, m_sensorChannels);
            SensorChannelsContext penSensorChannelsContext = SensorChannelsContext.CreateDefault(penInputProvider, m_currentInputDevice); //new SensorChannelsContext(penInputProvider, m_currentInputDevice, m_sensorChannels);

            // Cache sensor channels contexts
            m_sensorChannelsContexts.Add(mouseSensorChannelsContext.Id, mouseSensorChannelsContext);
            m_sensorChannelsContexts.Add(touchSensorChannelsContext.Id, touchSensorChannelsContext);
            m_sensorChannelsContexts.Add(penSensorChannelsContext.Id, penSensorChannelsContext);

            // Init sensor contexts
            SensorContext mouseSensorContext = new SensorContext();
            mouseSensorContext.AddSensorChannelsContext(mouseSensorChannelsContext);

            SensorContext touchSensorContext = new SensorContext();
            touchSensorContext.AddSensorChannelsContext(touchSensorChannelsContext);

            SensorContext penSensorContext = new SensorContext();
            penSensorContext.AddSensorChannelsContext(penSensorChannelsContext);

            // Cache sensor contexts
            m_sensorContexts.Add(mouseSensorContext.Id, mouseSensorContext);
            m_sensorContexts.Add(touchSensorContext.Id, touchSensorContext);
            m_sensorContexts.Add(penSensorContext.Id, penSensorContext);

            // Init input contexts
            InputContext mouseInputContext = new InputContext(mEnvironment.Id, mouseSensorContext.Id);
            InputContext touchInputContext = new InputContext(mEnvironment.Id, touchSensorContext.Id);
            InputContext penInputContext = new InputContext(mEnvironment.Id, penSensorContext.Id);

            // Cache input contexts
            m_inputContexts.Add(mouseInputContext.Id, mouseInputContext);
            m_inputContexts.Add(touchInputContext.Id, touchInputContext);
            m_inputContexts.Add(penInputContext.Id, penInputContext);

            m_deviceTypeMap.Add(PointerDeviceType.Mouse, mouseInputContext.Id);
            m_deviceTypeMap.Add(PointerDeviceType.Touch, touchInputContext.Id);
            m_deviceTypeMap.Add(PointerDeviceType.Pen, penInputContext.Id);
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
            EncodeStrokeCommon(stroke.Id, stroke.Spline.Clone(), stroke.Layout, stroke.SensorDataId, style);
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
            EncodeStrokeCommon(stroke.Id, stroke.Spline, stroke.Layout, stroke.SensorDataId, style);
        }

        public Identifier AddSensorData(PointerDeviceType deviceType, List<PointerData> pointerDataList)
        {
            Identifier inputContextId = m_deviceTypeMap[deviceType];
            InputContext inputContext = m_inputContexts[inputContextId];
            SensorContext sensorContext = m_sensorContexts[inputContext.SensorContextId];

            // Create sensor data using the input context
            SensorData sensorData = new SensorData(
                Identifier.FromNewGuid(),
                inputContext.Id,
                InkState.Plane);

            PopulateSensorData(sensorData, sensorContext, pointerDataList);

            m_sensorDataMap.TryAdd(sensorData.Id, sensorData);

            return sensorData.Id;
        }

        private void EncodeStrokeCommon(Identifier id, Spline spline , PathPointLayout layout, Identifier sensorDataId, Style style)
        {
            Stroke stroke = new Stroke(
                id,
                spline.Clone(),
                style,
                layout,
                sensorDataId);

            StrokeNode strokeNode = new StrokeNode(Identifier.FromNewGuid(), stroke);
            InkDocument.InkTree.Root.Add(strokeNode);

            if (sensorDataId != Identifier.Empty)
            {
                SensorData sensorData = m_sensorDataMap[sensorDataId];

                AddSensorDataToModel(sensorData);
            }
        }

        private void AddRasterBrushToInkDoc(PointerDeviceType deviceType, RasterBrush rasterBrush, Style rasterStyle, StrokeConstants strokeConstants, uint startRandomSeed)
        {
            rasterStyle.RenderModeUri = $"will3://rendering//{deviceType.ToString()}";

            if (!InkDocument.Brushes.TryGetBrush(rasterBrush.Name, out Brush foundBrush))
            {
                InkDocument.Brushes.AddRasterBrush(rasterBrush);
            } 
        }

        private void AddVectorBrushToInkDoc(string pointerDeviceType, Wacom.Ink.Serialization.Model.VectorBrush vectorBrush, Style style)
        {
            style.RenderModeUri = $"will3://rendering//{pointerDeviceType}";

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
            //inputDevice.Properties["dev.model"] = m_eas.SystemProductName;
            //inputDevice.Properties["dev.manufacturer"] = m_eas.SystemManufacturer;
            inputDevice.Seal();

            Identifier inputDeviceId = inputDevice.Id;
            bool res = InkDocument.InputConfiguration.Devices.Any((device) => device.Id == inputDeviceId);

            if (!res)
            {
                InkDocument.InputConfiguration.Devices.Add(inputDevice);
            }

            return inputDevice;
        }

        private SensorContext CreateAndAddSensorContext(InkInputProvider inkInputProvider, InputDevice inputDevice)
        {
            // Create the sensor channel groups using the input provider and device
            SensorChannelsContext defaultSensorChannelsContext = SensorChannelsContext.CreateDefault(inkInputProvider, inputDevice);

            SensorChannelsContext specialChannelsContext = new SensorChannelsContext(
                inkInputProvider,
                inputDevice,
                new List<SensorChannel> { mTimestampSensorChannel },//, mSpecialPressureSensorChannel },
                latency: 2,
                samplingRateHint: 2);

            // Create the sensor context using the sensor channels contexts
            SensorContext sensorContext = new SensorContext();
            sensorContext.AddSensorChannelsContext(defaultSensorChannelsContext);
            //sensorContext.AddSensorChannelsContext(specialChannelsContext);

            Identifier sensorContextId = sensorContext.Id;
            bool res = InkDocument.InputConfiguration.SensorContexts.Any((context) => context.Id == sensorContextId);

            if (!res)
            {
                InkDocument.InputConfiguration.SensorContexts.Add(sensorContext);
            }

            return sensorContext;
        }

        private Identifier CreateAndAddInputContext(Identifier sensorContextId)
        {
            InputContext inputContext = new InputContext(mEnvironment.Id, sensorContextId);

            Identifier inputContextId = inputContext.Id;
            bool res = InkDocument.InputConfiguration.InputContexts.Any((context) => context.Id == inputContextId);

            if (!res)
            {
                InkDocument.InputConfiguration.InputContexts.Add(inputContext);
            }

            return inputContextId;
        }

        private void FillDefaultChannels(SensorData sensorData, SensorContext sensorContext, List<PointerData> pointerDataList)
        {
            SensorChannelsContext channels = sensorContext.DefaultSensorChannelsContext;

            sensorData.AddData(channels.GetChannel(InkSensorType.X), pointerDataList.Select((pd) => pd.X).ToList());
            sensorData.AddData(channels.GetChannel(InkSensorType.Y), pointerDataList.Select((pd) => pd.Y).ToList());
            sensorData.AddTimestampData(channels.GetChannel(InkSensorType.Timestamp), pointerDataList.Select((pd) => pd.Timestamp).ToList());

            if (pointerDataList[0].Force.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Pressure), pointerDataList.Select((pd) => pd.Force.Value).ToList());
            }

            if (pointerDataList[0].Radius.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.RadiusX), pointerDataList.Select((pd) => pd.Radius.Value).ToList());
            }

            if (pointerDataList[0].AzimuthAngle.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Azimuth), pointerDataList.Select((pd) => pd.AzimuthAngle.Value).ToList());
            }

            if (pointerDataList[0].AltitudeAngle.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Altitude), pointerDataList.Select((pd) => pd.AltitudeAngle.Value).ToList());
            }
        }

        private void PopulateSensorData(SensorData sensorData, SensorContext sensorContext, List<PointerData> pointerDataList)
        {
            SensorChannelsContext channels = sensorContext.DefaultSensorChannelsContext;

            sensorData.AddData(channels.GetChannel(InkSensorType.X), pointerDataList.Select((pd) => pd.X).ToList());
            sensorData.AddData(channels.GetChannel(InkSensorType.Y), pointerDataList.Select((pd) => pd.Y).ToList());
            sensorData.AddTimestampData(channels.GetChannel(InkSensorType.Timestamp), pointerDataList.Select((pd) => pd.Timestamp).ToList());

            if (pointerDataList[0].Force.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Pressure), pointerDataList.Select((pd) => pd.Force.Value).ToList());
            }

            if (pointerDataList[0].Radius.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.RadiusX), pointerDataList.Select((pd) => pd.Radius.Value).ToList());
            }

            if (pointerDataList[0].AzimuthAngle.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Azimuth), pointerDataList.Select((pd) => pd.AzimuthAngle.Value).ToList());
            }

            if (pointerDataList[0].AltitudeAngle.HasValue)
            {
                sensorData.AddData(channels.GetChannel(InkSensorType.Altitude), pointerDataList.Select((pd) => pd.AltitudeAngle.Value).ToList());
            }
        }

        private void AddSensorDataToModel(SensorData sensorData)
        {
            // Add sensor data if missing
            if (!InkDocument.SensorData.ContainsId(sensorData.Id))
            {
                InkDocument.SensorData.Add(sensorData);

                InputContext inputContext = m_inputContexts[sensorData.InputContextID];

                // Add input context if missing
                if (!InkDocument.InputConfiguration.InputContexts.Contains(inputContext))
                {
                    InkDocument.InputConfiguration.InputContexts.Add(inputContext);

                    Wacom.Ink.Serialization.Model.Environment environment = m_environments[inputContext.EnvironmentId];

                    // Add environment if missing
                    if (!InkDocument.InputConfiguration.Environments.Contains(environment))
                    {
                        InkDocument.InputConfiguration.Environments.Add(environment);
                    }

                    SensorContext sensorContext = m_sensorContexts[inputContext.SensorContextId];

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
            if (m_sensorDataMap.TryAdd(sensorData.Id, sensorData))
            {
                InputContext inputContext = inkModel.InputConfiguration.InputContexts.Find((ic) => ic.Id == sensorData.InputContextID);

                // Load input context if missing
                if (m_inputContexts.TryAdd(inputContext.Id, inputContext))
                {
                    Wacom.Ink.Serialization.Model.Environment environment = inkModel.InputConfiguration.Environments.Find((env) => env.Id == inputContext.EnvironmentId);
                    m_environments.TryAdd(environment.Id, environment);

                    SensorContext sensorContext = inkModel.InputConfiguration.SensorContexts.Find((sc) => sc.Id == inputContext.SensorContextId);

                    // Load sensor context if missing
                    if (m_sensorContexts.TryAdd(sensorContext.Id, sensorContext))
                    {
                        // Iterate and load sensor channels contexts if missing
                        for (int j = 0; j < sensorContext.SensorChannelContexts.Count; j++)
                        {
                            SensorChannelsContext sensorChannelsContext = sensorContext.SensorChannelContexts[j];

                            // Load input device if missing
                            m_inputDevices.TryAdd(sensorChannelsContext.InputDevice.Id, sensorChannelsContext.InputDevice);

                            // Load ink input provider if missing
                            m_inkInputProvidersMap.TryAdd(sensorChannelsContext.InkInputProvider.Id, sensorChannelsContext.InkInputProvider);
                        }
                    }
                }
            }
        }

    }
}
