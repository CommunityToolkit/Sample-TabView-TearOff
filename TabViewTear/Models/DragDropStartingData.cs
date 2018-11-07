﻿using System.Collections.Generic;

using Windows.ApplicationModel.DataTransfer;

namespace TabViewTear.Models
{
    public class DragDropStartingData
    {
        public DataPackage Data { get; set; }

        public IList<object> Items { get; set; }
    }
}
