using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using System.Collections.Generic;
using System.Linq;
using System; 
using System.Windows.Threading; 
using TrafficSimulator.Models; 

namespace TrafficSimulator
{
    public partial class MainWindow : Window
    {
        private Graph _graph = new Graph();
        private int _nodeCounter = 0; // Para generar IDs únicos automáticamente
        private Node _selectedNodeForConnection = null; // Para conectar aristas
        private List<Vehicle> _vehicles = new List<Vehicle>();
        private DispatcherTimer _simulationTimer;
        private Random _random = new Random();
        private int _vehicleCounter = 0; // Para dar IDs únicos a los vehículos
        private double _simulationSpeed = 1.0; // Factor para acelerar/desacelerar la simulación (1.0 = normal)
        private const double BaseVehicleSpeed = 50; // Pixels por segundo (la velocidad "real" del vehículo)
        private TimeSpan _simulationTickInterval = TimeSpan.FromMilliseconds(50); // Frecuencia de actualización de la simulación

        public MainWindow()
        {
            InitializeComponent();
            InitializeSimulationTimer();
        }
        private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            // El 'sender' es el Slider que disparó el evento
            Slider slider = sender as Slider;
            if (slider != null)
            {
                // Actualizar la variable _simulationSpeed con el nuevo valor del slider
                _simulationSpeed = slider.Value;
                // El TextBlock ya se actualiza automáticamente gracias al Binding en XAML.
            }
        }
        private void InitializeSimulationTimer()
        {
            _simulationTimer = new DispatcherTimer();
            _simulationTimer.Interval = _simulationTickInterval; // Actualizar cada 50 ms
            _simulationTimer.Tick += SimulationTimer_Tick;
            // _simulationTimer.Start(); // No iniciar automáticamente, el usuario lo hará con un botón
        }

        private void SimulationTimer_Tick(object sender, EventArgs e)
        {
            // Generar nuevos vehículos aleatoriamente
            if (_random.Next(1, 100) < 5)
            {
                GenerateRandomVehicle();
            }

            List<Vehicle> vehiclesToRemove = new List<Vehicle>();
            foreach (var vehicle in _vehicles)
            {
                double actualSpeedPerTick = BaseVehicleSpeed * _simulationSpeed * _simulationTickInterval.TotalSeconds;

                // Ahora, UpdatePosition devuelve la nueva posición y si ha llegado
                (double newX, double newY, bool hasArrived) = vehicle.UpdatePosition(actualSpeedPerTick);

                // Actualizar la posición del UIElement del vehículo en el Canvas desde MainWindow
                if (vehicle.UIEllipse != null)
                {
                    // Centrar el vehículo en la posición calculada
                    Canvas.SetLeft(vehicle.UIEllipse, newX - vehicle.UIEllipse.Width / 2);
                    Canvas.SetTop(vehicle.UIEllipse, newY - vehicle.UIEllipse.Height / 2);
                }

                if (hasArrived)
                {
                    vehiclesToRemove.Add(vehicle);
                }
            }

            foreach (var vehicle in vehiclesToRemove)
            {
                _vehicles.Remove(vehicle);
                GraphCanvas.Children.Remove(vehicle.UIEllipse);
            }
        }

        private void GenerateRandomVehicle()
        {
            if (_graph.Nodes.Count < 2)
            {
                // No hay suficientes nodos para crear una ruta
                return;
            }

            // Seleccionar nodos de inicio y fin aleatorios
            Node startNode = _graph.Nodes[_random.Next(_graph.Nodes.Count)];
            Node endNode = _graph.Nodes[_random.Next(_graph.Nodes.Count)];

            // Asegurarse de que el nodo de inicio no sea el mismo que el de destino
            while (startNode == endNode)
            {
                endNode = _graph.Nodes[_random.Next(_graph.Nodes.Count)];
            }

            // Calcular la ruta más corta
            var dijkstraResult = _graph.CalculateShortestPaths(startNode);
            List<Node> path = _graph.GetShortestPath(startNode, endNode, dijkstraResult);

            if (path == null || path.Count < 2)
            {
                // No se encontró un camino válido entre los nodos
                // Esto podría ocurrir si el grafo no está conectado
                Console.WriteLine($"No path found from {startNode.Id} to {endNode.Id}");
                return;
            }

            // Crear el vehículo
            _vehicleCounter++;
            Brush vehicleColor = GetRandomColor();
            Vehicle newVehicle = new Vehicle($"V{_vehicleCounter}", startNode, endNode, path, BaseVehicleSpeed, vehicleColor);

            _vehicles.Add(newVehicle);

            // Añadir el vehículo a la UI en la posición inicial del nodo de inicio
            Canvas.SetLeft(newVehicle.UIEllipse, startNode.X - newVehicle.UIEllipse.Width / 2);
            Canvas.SetTop(newVehicle.UIEllipse, startNode.Y - newVehicle.UIEllipse.Height / 2);
            GraphCanvas.Children.Add(newVehicle.UIEllipse);
        }

        private Brush GetRandomColor()
        {
            // Colores más amigables para vehículos
            var colors = new List<Brush>
            {
                Brushes.Green, Brushes.Orange, Brushes.Purple, Brushes.Teal, Brushes.DarkCyan,
                Brushes.Brown, Brushes.Indigo, Brushes.Olive, Brushes.SaddleBrown
            };
            return colors[_random.Next(colors.Count)];
        }
        private void GraphCanvas_MouseDown(object sender, MouseButtonEventArgs e)
        {
            if (e.LeftButton == MouseButtonState.Pressed && _selectedNodeForConnection == null)
            {
                // Obtener las coordenadas del clic relativas al Canvas
                Point clickPoint = e.GetPosition(GraphCanvas);

                // Pedir al usuario un nombre para el nodo
                string nodeId = Microsoft.VisualBasic.Interaction.InputBox("Introduzca el nombre del nodo:", "Añadir nuevo nodo", $"Nodo{_nodeCounter + 1}");

                if (string.IsNullOrWhiteSpace(nodeId))
                {
                    MessageBox.Show("El nombre del nodo no puede ser vacío.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                // Verificar si el nombre ya existe
                if (_graph.Nodes.Any(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Nodo wcon el nombre '{nodeId}' ya existe. Porfavor seleccione un nombre diferente.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
        // Variable para almacenar la arista sobre la que se hizo clic derecho
        private Edge _contextMenuEdge = null;

        private void Edge_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Line clickedLine = sender as Line;
            if (clickedLine != null)
            {
                // Encontrar la arista del modelo asociada a esta línea UI
                _contextMenuEdge = _graph.Edges.FirstOrDefault(ed => ed.UILine == clickedLine);

                if (_contextMenuEdge != null)
                {
                    // Crear el menú contextual
                    ContextMenu cm = new ContextMenu();
                    MenuItem deleteItem = new MenuItem { Header = "Borrar arista" };
                    deleteItem.Click += DeleteEdge_Click; // Asignar el evento Click al MenuItem
                    cm.Items.Add(deleteItem);

                    clickedLine.ContextMenu = cm; // Asignar el ContextMenu a la línea
                    cm.IsOpen = true; // Abrir el menú contextual
                }
            }
        }

        private void DeleteEdge_Click(object sender, RoutedEventArgs e)
        {
            // Este método se llama cuando el usuario hace clic en "Borrar arista"
            if (_contextMenuEdge != null)
            {
                // Confirmación (opcional, pero buena práctica)
                MessageBoxResult result = MessageBox.Show(
                    $"Are you sure you want to delete the edge between {_contextMenuEdge.Source.Id} and {_contextMenuEdge.Destination.Id}?",
                    "Confirm Deletion",
                    MessageBoxButton.YesNo,
                    MessageBoxImage.Warning);

                if (result == MessageBoxResult.Yes)
                {
                    // Eliminar la arista del modelo (y su contraparte si es bidireccional)
                    _graph.Edges.Remove(_contextMenuEdge);

                    // También eliminar la arista bidireccional si existe
                    Edge reverseEdge = _graph.Edges.FirstOrDefault(ed =>
                        ed.Source == _contextMenuEdge.Destination && ed.Destination == _contextMenuEdge.Source);
                    if (reverseEdge != null)
                    {
                        _graph.Edges.Remove(reverseEdge);
                        GraphCanvas.Children.Remove(reverseEdge.UILine); // Eliminar también la línea inversa de la UI
                    }

                    // Eliminar la línea de la UI
                    GraphCanvas.Children.Remove(_contextMenuEdge.UILine);

                    // Limpiar la referencia
                    _contextMenuEdge = null;

                    // Opcional: Recalcular rutas para vehículos existentes si las rutas se invalidaron
                    // (Esto se haría en un requerimiento posterior, pero es una consideración importante)
                }
            }
        }
        private void DrawNode(Node node)
        {
            // Dibujar un círculo para el nodo
            Ellipse nodeShape = new Ellipse
            {
                Width = 60,
                Height = 60,
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
                            string weightStr = Microsoft.VisualBasic.Interaction.InputBox("Introduzca un peso (e.g., distancia o tiempo):", "Peso de arista", "100");
                            if (!double.TryParse(weightStr, out double weight) || weight <= 0)
                            {
                                MessageBox.Show("Peso invalido. Usando predeterminado de 100.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                weight = 100;
                            }

                            Edge newEdge = new Edge(_selectedNodeForConnection, destinationNode, weight); 

                            _graph.AddEdge(newEdge);

                            // Dibujar la arista
                            DrawEdge(newEdge);
                        }
                        else
                        {
                            MessageBox.Show("Una arista ya existe entre estos dos nodos", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }
                if (_selectedNodeForConnection != null)
                {
                    // Deshacer el resaltado y resetear la selección
                    _selectedNodeForConnection.UIEllipse.Stroke = Brushes.DarkBlue;
                    _selectedNodeForConnection.UIEllipse.StrokeThickness = 2;
                    _selectedNodeForConnection = null;
                }
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

            edgeLine.MouseRightButtonUp += Edge_MouseRightButtonUp;

            TextBlock weightText = new TextBlock
            {
                Text = edge.Weight.ToString("F0"), // Formatear el peso sin decimales (o "F1" para uno)
                Foreground = Brushes.Red,         // Color del texto (puedes elegir otro)
                Background = Brushes.LightYellow, // Fondo para que sea más legible sobre la línea
                Padding = new Thickness(2),
            };
            double centerX = (edge.Source.X + edge.Destination.X) / 2;
            double centerY = (edge.Source.Y + edge.Destination.Y) / 2;

            Canvas.SetLeft(weightText, centerX - (weightText.ActualWidth / 2));
            Canvas.SetTop(weightText, centerY - (weightText.ActualHeight / 2) - 10);

            GraphCanvas.Children.Insert(0, edgeLine);
            GraphCanvas.Children.Add(weightText);

            edge.UILine = edgeLine; 
            edge.WeightTextBlock = weightText;
        }

        private void AddNode_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("La función de añadir nodos mediante un botón aún no está implementada. Haga clic en el lienzo para añadir un nodo.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void ConnectNodes_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show("La conexión de nodos mediante botones aún no está implementada. Haga clic y arrastre entre nodos para conectarlos.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private void GraphCanvas_MouseUp(object sender, MouseButtonEventArgs e)
        {
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
        private void ToggleSimulation_Click(object sender, RoutedEventArgs e)
        {
            if (_simulationTimer.IsEnabled)
            {
                _simulationTimer.Stop();
                (sender as Button).Content = "Empezar Simulación";
            }
            else
            {
                if (_graph.Nodes.Count < 2)
                {
                    MessageBox.Show("Porfavor cree al menos dos nodos y una arista antes de comenzar la simulación.", "Peligro", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                _simulationTimer.Start();
                (sender as Button).Content = "Parar Simulación";
            }
        }
    }
}

