using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ANNIndexingSample.Entities
{
    internal class MovieCentroid
    {
        public int ClusterId { get; set; }
        public float[]? CentroidVector { get; set; }
        public int MemberCount { get; set; }
    }
}
