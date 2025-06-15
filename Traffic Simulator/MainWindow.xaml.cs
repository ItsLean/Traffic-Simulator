using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq; // Para el .Any()

// Asegúrate de tener los namespaces correctos para tus modelos
using TrafficSimulator.Models; // Suponiendo que tus modelos están en una carpeta Models

namespace TrafficSimulator
{
    public partial class MainWindow : Window
    {
        private Graph _graph = new Graph();
        private int _nodeCounter = 0; // Para generar IDs únicos automáticamente
        private Node _selectedNodeForConnection = null; // Para conectar aristas

        public MainWindow()
        {
            InitializeComponent();
        }

        // --- Agregar Nodos (ID 002) ---

        // Método que se llama cuando el usuario hace clic en el lienzo para agregar un nodo
        private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _selectedNodeForConnection == null)
            {
                // Obtener las coordenadas del clic relativas al Canvas
                Point clickPoint = e.GetPosition(GraphCanvas);

                // Pedir al usuario un nombre para el nodo
                string nodeId = Microsoft.VisualBasic.Interaction.InputBox("Enter Node Name:", "Add New Node", $"Node{_nodeCounter + 1}");

                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    MessageBox.Show("Node name cannot be empty.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Verificar si el nombre ya existe
                if (_graph.Nodes.Any(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Node with name '{nodeId}' already exists. Please choose a unique name.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Crear el nodo en nuestro modelo
                Node newNode = new Node(nodeId, clickPoint.X, clickPoint.Y);
                _graph.AddNode(newNode);
                _nodeCounter++;

                // Dibujar el nodo en el Canvas
                DrawNode(newNode);
            }
        }

        private void DrawNode(Node node)
        {
            // Dibujar un círculo para el nodo
            Ellipse nodeShape = new Ellipse
            {
                Width = 40,
                Height = 40,
                Fill = Brushes.Blue,
                Stroke = Brushes.DarkBlue,
                StrokeThickness = 2
            };

            // Centrar el nodo en las coordenadas de clic
            Canvas.SetLeft(nodeShape, node.X - nodeShape.Width / 2);
            Canvas.SetTop(nodeShape, node.Y - nodeShape.Height / 2);

            // Almacenar referencia al UIElement en el modelo Node
            node.UIEllipse = nodeShape;

            // Manejar eventos de clic en el nodo para seleccionarlo para conexión
            nodeShape.MouseDown += Node_MouseDown;
            nodeShape.MouseUp += Node_MouseUp; // Puede ser útil si implementas arrastrar y soltar nodos

            GraphCanvas.Children.Add(nodeShape);

            // Agregar una etiqueta de texto para el nombre del nodo
            TextBlock nodeLabel = new TextBlock
            {
                Text = node.Id,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center,
                VerticalAlignment = VerticalAlignment.Center,
                FontSize = 10,
                IsHitTestVisible = false // Para que los clics pasen al Ellipse de abajo
            };
            node.UILable = nodeLabel;

            // Posicionar la etiqueta sobre el nodo
            Canvas.SetLeft(nodeLabel, node.X - (node.Id.Length * 3)); // Ajuste aproximado
            Canvas.SetTop(nodeLabel, node.Y - nodeShape.Height / 2 + (nodeShape.Height / 2 - nodeLabel.FontSize / 2));
            GraphCanvas.Children.Add(nodeLabel);
        }

        // --- Conectar Nodos (ID 002) ---

        private void Node_MouseDown(object sender, MouseButtonEventArgs e)
        {
            // Detectar si se hizo clic en un nodo para empezar una conexión
            if (e.LeftButton == MouseButtonState.Pressed)
            {
                Ellipse clickedShape = sender as Ellipse;
                if (clickedShape != null)
                {
                    // Encontrar el nodo del modelo asociado a esta forma UI
                    _selectedNodeForConnection = _graph.Nodes.FirstOrDefault(n => n.UIEllipse == clickedShape);
                    if (_selectedNodeForConnection != null)
                    {
                        // Resaltar el nodo seleccionado
                        _selectedNodeForConnection.UIEllipse.Stroke = Brushes.Red;
                        _selectedNodeForConnection.UIEllipse.StrokeThickness = 4;
                        e.Handled = true; // Indicar que el evento ha sido manejado
                    }
                }
            }
        }

        private void Node_MouseUp(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Released && _selectedNodeForConnection != null)
            {
                Ellipse releasedShape = sender as Ellipse;
                if (releasedShape != null)
                {
                    Node destinationNode = _graph.Nodes.FirstOrDefault(n => n.UIEllipse == releasedShape);

                    if (destinationNode != null && destinationNode != _selectedNodeForConnection)
                    {
                        // Hemos soltado el mouse sobre otro nodo, intentar crear una arista
                        // Verificar si la arista ya existe (bidireccional)
                        bool edgeExists = _graph.Edges.Any(ed =>
                            (ed.Source == _selectedNodeForConnection && ed.Destination == destinationNode) ||
                            (ed.Source == destinationNode && ed.Destination == _selectedNodeForConnection));

                        if (!edgeExists)
                        {
                            // Crear la arista en el modelo
                            Edge newEdge = new Edge(_selectedNodeForConnection, destinationNode);
                            _graph.AddEdge(newEdge);

                            // Dibujar la arista
                            DrawEdge(newEdge);
                        }
                        else
                        {
                            MessageBox.Show("An edge already exists between these two nodes.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }

                // Deshacer el resaltado y resetear la selección
                _selectedNodeForConnection.UIEllipse.Stroke = Brushes.DarkBlue;
                _selectedNodeForConnection.UIEllipse.StrokeThickness = 2;
                _selectedNodeForConnection = null;
            }
        }

        private void DrawEdge(Edge edge)
        {
            Line edgeLine = new Line
            {
                X1 = edge.Source.X,
                Y1 = edge.Source.Y,
                X2 = edge.Destination.X,
                Y2 = edge.Destination.Y,
                Stroke = Brushes.Black,
                StrokeThickness = 2
            };

            // Añadir la línea al Canvas. Es importante añadirla ANTES de los nodos para que queden encima.
            GraphCanvas.Children.Insert(0, edgeLine); // Insertar al principio de la lista de hijos

            // Almacenar referencia al UIElement en el modelo Edge
            edge.UILine = edgeLine;
        }

        // --- Para el cuadro de diálogo de entrada (Microsoft.VisualBasic) ---
        // Necesitarás añadir una referencia al ensamblado 'Microsoft.VisualBasic'
        // Clic derecho en 'Referencias' o 'Dependencias' en tu proyecto en Visual Studio -> 'Add Project Reference...'
        // Busca y marca 'Microsoft.VisualBasic' bajo la pestaña 'Assemblies' -> 'Framework' o 'All'.
        // ... (tu código existente de MainWindow.xaml.cs) ...

        // Métodos para los botones (actualmente vacíos, se implementarán después)
        private void AddNode_Click(object sender, RoutedEventArgs e)
        {
            // Aquí iría la lógica específica para un botón de añadir nodo,
            // si el usuario no lo va a añadir directamente con un click en el lienzo.
            // Por ahora, no hace nada. La creación de nodos sigue siendo por clic en el lienzo.
            MessageBox.Show("Adding node via button is not yet implemented. Click on the canvas to add a node.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConnectNodes_Click(object sender, RoutedEventArgs e)
        {
            // Aquí iría la lógica para iniciar el modo de conexión de nodos si fuera necesario
            // Por ahora, la conexión se hace arrastrando de un nodo a otro.
            MessageBox.Show("Connecting nodes via button is not yet implemented. Click and drag between nodes to connect.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        // Método MouseUp para el Canvas (actualmente ya lo tenemos en Node_MouseUp,
        // pero este es para si soltaras el ratón en el canvas fuera de un nodo)
        private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
            // Si habías seleccionado un nodo para conectar y lo sueltas fuera de otro nodo,
            // debes deseleccionar el nodo y quitar el resaltado.
            if (_selectedNodeForConnection != null)
            {
                _selectedNodeForConnection.UIEllipse.Stroke = Brushes.DarkBlue;
                _selectedNodeForConnection.UIEllipse.StrokeThickness = 2;
                _selectedNodeForConnection = null;
            }
        }

        // Método MouseMove para el Canvas (útil para arrastrar nodos o dibujar líneas de previsualización)
        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
            // Lógica para arrastrar nodos o para dibujar una línea de previsualización
            // cuando estás conectando nodos (opcional, para una mejor UX).
            // Por ahora, no hace nada visual.
        }
    }
}
    
