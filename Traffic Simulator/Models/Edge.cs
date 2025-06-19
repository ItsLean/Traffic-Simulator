using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace TrafficSimulator.Models
{
    public class Edge
    {
        public Node Source { get; set; }
        public Node Destination { get; set; }
        public double Weight { get; set; } // Peso de la arista (distancia, tiempo, etc.)
        public Line UILine { get; set; }
        public TextBlock WeightTextBlock { get; set; }

        public Edge(Node source, Node destination, double weight = 1)
        {
            Source = source;
            Destination = destination;
            Weight = weight;
        }
    }
}
