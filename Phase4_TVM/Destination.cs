using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace TVM
{
    public class Destination
    {
        public string destinationId
        { get; set; }

        public string name
        { get; set; }

        public Fare Fare
        { get; set; }
    }
}