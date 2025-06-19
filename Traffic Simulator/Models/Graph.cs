using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows.Controls; // Necesario para TextBlock, si Node.cs lo usa.
using System.Windows.Shapes;   // Necesario para Ellipse, si Node.cs lo usa.

namespace TrafficSimulator.Models
{
    public class Graph
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
            if (Nodes.Any(n => n.Id == node.Id))
            {
                throw new ArgumentException($"Node with ID '{node.Id}' already exists.");
            }
            Nodes.Add(node);
        }

        public void AddEdge(Edge edge)
        {
            if (!Nodes.Contains(edge.Source) || !Nodes.Contains(edge.Destination))
            {
                throw new ArgumentException("Source or Destination node not found in graph.");
            }
            Edges.Add(edge);

            // IMPORTANTE: Para este simulador de tráfico, las carreteras suelen ser bidireccionales.
            // Si una carretera va de A a B, también se puede ir de B a A.
            // Si las aristas son unidireccionales, omite esta parte.
            // Aquí asumimos bidireccional, pero puedes modificarlo si es necesario.
            if (!Edges.Any(e => e.Source == edge.Destination && e.Destination == edge.Source))
            {
                Edges.Add(new Edge(edge.Destination, edge.Source, edge.Weight));
            }
        }

        // --- Implementación del Algoritmo de Dijkstra ---

        // Clase auxiliar para los resultados de Dijkstra
        public class DijkstraResult
        {
            public Dictionary<Node, double> Distances { get; set; } // Distancia desde el origen a cada nodo
            public Dictionary<Node, Node> PreviousNodes { get; set; } // Nodo previo en el camino más corto
        }

        public DijkstraResult CalculateShortestPaths(Node startNode)
        {
            var distances = new Dictionary<Node, double>();
            var previousNodes = new Dictionary<Node, Node>();
            var unvisitedNodes = new HashSet<Node>(Nodes); // Conjunto de nodos no visitados
            var priorityQueue = new SortedList<double, Node>(); // Cola de prioridad para seleccionar el próximo nodo

            // Inicializar distancias: infinito para todos, 0 para el nodo de inicio
            foreach (var node in Nodes)
            {
                distances[node] = double.PositiveInfinity;
                previousNodes[node] = null;
            }
            distances[startNode] = 0;
            priorityQueue.Add(0, startNode); // Agregar el nodo de inicio a la cola de prioridad

            while (priorityQueue.Count > 0)
            {
                // Obtener el nodo con la distancia más pequeña de la cola de prioridad
                var currentNodeDistance = priorityQueue.Keys[0];
                var currentNode = priorityQueue.Values[0];
                priorityQueue.RemoveAt(0); // Eliminarlo de la cola

                // Si ya visitamos este nodo con una distancia más corta, lo ignoramos
                // (Esto puede pasar si ya lo procesamos y luego lo actualizamos en la cola)
                if (currentNodeDistance > distances[currentNode])
                {
                    continue;
                }

                // Si el nodo ya fue procesado o no está en el conjunto de no visitados, continuar
                if (!unvisitedNodes.Contains(currentNode))
                {
                    continue;
                }

                unvisitedNodes.Remove(currentNode); // Marcar como visitado

                // Iterar sobre los vecinos del nodo actual
                // Los "vecinos" son los nodos a los que se puede llegar directamente desde currentNode
                var neighbors = Edges.Where(e => e.Source == currentNode);

                foreach (var edge in neighbors)
                {
                    var neighbor = edge.Destination;
                    if (unvisitedNodes.Contains(neighbor))
                    {
                        var newDistance = distances[currentNode] + edge.Weight;

                        if (newDistance < distances[neighbor])
                        {
                            distances[neighbor] = newDistance;
                            previousNodes[neighbor] = currentNode;

                            double uniqueDistance = newDistance;

                            int counter = 0;
                            while (priorityQueue.ContainsKey(uniqueDistance))
                            {
                                uniqueDistance = newDistance + (counter * 0.00000000001); // Añade un valor muy pequeño para hacer la clave única
                                counter++;
                            }
                            priorityQueue.Add(uniqueDistance, neighbor);
                        }
                    }
                }
            }

            return new DijkstraResult { Distances = distances, PreviousNodes = previousNodes };
        }

        // Método para reconstruir la ruta más corta desde los resultados de Dijkstra
        public List<Node> GetShortestPath(Node startNode, Node endNode, DijkstraResult dijkstraResult)
        {
            var path = new List<Node>();
            var currentNode = endNode;

            while (currentNode != null && dijkstraResult.PreviousNodes.ContainsKey(currentNode))
            {
                path.Insert(0, currentNode); // Inserta al inicio para construir el camino en orden
                currentNode = dijkstraResult.PreviousNodes[currentNode];
                if (currentNode == startNode) // Asegurarse de añadir el nodo de inicio
                {
                    path.Insert(0, startNode);
                    break;
                }
            }

            // Si el camino no comienza con el startNode, significa que no hay camino
            if (path.Count == 0 || path[0] != startNode)
            {
                return null; // No se encontró un camino
            }

            return path;
        }

        // --- Fin del Algoritmo de Dijkstra ---
    }
}