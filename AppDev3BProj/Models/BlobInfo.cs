using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;

namespace AppDev3BProj.Models
{
    public class BlobInfo
    {
        public string ImageUri { get; set; }
        public string ThumbnailUri { get; set; }
        public string Caption { get; set; }
        public string Height { get; set; }
        public string Width { get; set; }
        public string Format { get; set; }
        public string BlackWhite { get; set; }
        public string ForegroundColor { get; set; }
        public string BackgroundColor { get; set; }
        public string Colors { get; set; }
        public string Tags { get; set; }
        //public string Time { get; set; }
    }
}
