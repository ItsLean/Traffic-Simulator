﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Shapes; 
using System.Collections.Generic;
using System.Windows.Media;
using System.Windows.Controls;

namespace TrafficSimulator.Models
{
    public class Vehicle
    {
        public string Id { get; set; }
        public Node StartNode { get; private set; }
        public Node EndNode { get; private set; }
        public List<Node> Path { get;  set; }
        public double Speed { get; set; }
        public Node CurrentNode { get; set; }
        public bool HasReachedDestination { get; set; } = false;
        public int CurrentPathSegmentIndex { get; set; }
        public double DistanceAlongSegment { get; set; }
        public System.Windows.Shapes.Ellipse UIEllipse { get; set; }
        public bool IsSelected { get; set; }
        public List<Line> PathUILines { get; set; }

        public Vehicle(string id, Node start, Node end, List<Node> path, double speed, Brush color)
        {
            Id = id;
            StartNode = start;
            EndNode = end;
            Path = path;
            Speed = speed;
            CurrentPathSegmentIndex = 0;
            DistanceAlongSegment = 0;

            UIEllipse = new System.Windows.Shapes.Ellipse 
            {
                Width = 15,
                Height = 15,
                Fill = color,
                Stroke = Brushes.Black,
                StrokeThickness = 1
            };
        }

        public (double newX, double newY, bool hasArrived) UpdatePosition(double deltaTime)
        {
            if (Path == null || Path.Count < 2 || CurrentPathSegmentIndex >= Path.Count - 1)
            {
                // Ya llegó a su destino o no tiene un camino válido
                return (EndNode.X, EndNode.Y, true); // Devuelve la posición final y true
            }

            Node currentNode = Path[CurrentPathSegmentIndex];
            Node nextNode = Path[CurrentPathSegmentIndex + 1];

            double segmentLength = CalculateDistance(currentNode.X, currentNode.Y, nextNode.X, nextNode.Y);

            double distanceToMove = Speed * deltaTime;
                
            DistanceAlongSegment += distanceToMove;

            if (DistanceAlongSegment >= segmentLength)
            {
                // Ha llegado al siguiente nodo
                CurrentPathSegmentIndex++;
                DistanceAlongSegment = 0;

                if (CurrentPathSegmentIndex >= Path.Count - 1)
                {
                    // Ha llegado al nodo final
                    return (nextNode.X, nextNode.Y, true); // Devuelve la posición final y true
                }
            }

            // Calcular la posición interpolada a lo largo del segmento
            double progress = DistanceAlongSegment / segmentLength;
            double currentX = currentNode.X + (nextNode.X - currentNode.X) * progress;
            double currentY = currentNode.Y + (nextNode.Y - currentNode.Y) * progress;

            return (currentX, currentY, false); // Devuelve la nueva posición y false
        }

        private double CalculateDistance(double x1, double y1, double x2, double y2)
        {
            return Math.Sqrt(Math.Pow(x2 - x1, 2) + Math.Pow(y2 - y1, 2));
        }
    }
}