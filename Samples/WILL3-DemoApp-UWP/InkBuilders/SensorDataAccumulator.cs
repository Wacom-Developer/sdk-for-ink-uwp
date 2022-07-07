using System.Collections.Generic;
using Wacom.Ink.Geometry;

namespace WacomInkDemoUWP
{
    public class SensorDataAccumulator : IInkDataProcessor
    {
        private IInkDataProvider<List<PointerData>> m_source;
        private readonly List<PointerData> m_data = new List<PointerData>();

        public void Process()
        {
            m_data.AddRange(m_source.Addition);
        }

        public void Reset()
        {
            m_data.Clear();
        }

        public void SetDataProvider(IInkDataProvider dataProvider)
        {
            m_source = dataProvider as IInkDataProvider<List<PointerData>>;
        }

        public void SetDataProvider(IInkDataProvider<List<PointerData>> dataProvider)
        {
            m_source = dataProvider;
        }

        public IInkDataProvider DataProvider
        {
            get => m_source;
        }

    }
}
