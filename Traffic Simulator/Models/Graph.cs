using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TrafficSimulator.Models
{
    class Graph
    {
        public List<Node> Nodes { get; private set; }
        public List<Edge> Edges { get; private set; }

        public Graph()
        {
            Nodes = new List<Node>();
            Edges = new List<Edge>();
        }

        public void AddNode(Node node)
        {
            // Asegurarse de que el ID sea único
            if (Nodes.Any(n => n.Id == node.Id))
            {
                throw new ArgumentException($"Node with ID '{node.Id}' already exists.");
            }
            Nodes.Add(node);
        }

        public void AddEdge(Edge edge)
        {
            // Opcional: Verificar que los nodos existan y que no haya una arista duplicada
            if (!Nodes.Contains(edge.Source) || !Nodes.Contains(edge.Destination))
            {
                throw new ArgumentException("Source or Destination node not found in graph.");
            }
            Edges.Add(edge);
        }
    }
}
