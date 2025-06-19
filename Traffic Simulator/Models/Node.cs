using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Shapes;

namespace TrafficSimulator.Models
{
    public class Node
    {
        public string Id { get; set; } // Nombre único del nodo (ciudad)
        public double X { get; set; }  // Coordenada X para la visualización
        public double Y { get; set; }  // Coordenada Y para la visualización

        public Ellipse UIEllipse { get; set; }
        public TextBlock UILabel { get; set; }
        public UIElement UIContainer { get; set; }
        public Node(string id, double x, double y)
        {
            Id = id;
            X = x;
            Y = y;
        }
    }
}
