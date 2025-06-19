using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Shapes;
using Microsoft.VisualBasic;
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
        private const double BaseVehicleSpeed = 60; // Pixels por segundo (la velocidad "real" del vehículo)
        private TimeSpan _simulationTickInterval = TimeSpan.FromMilliseconds(100); // Frecuencia de actualización de la simulación
        private Vehicle _selectedVehicle = null;
        private const double SelectedPathThickness = 5;
        private static readonly Brush SelectedPathColor = Brushes.LimeGreen;
        private Grid _selectedNodeContainer = null;
        private bool _isDraggingNode = false;
        private Point _lastMousePosition;
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
                    HighlightVehiclePath(vehicle, false);
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
                Console.WriteLine($"No se encontró ningún camino desde {startNode.Id} a {endNode.Id}");
                return;
            }

            // Crear el vehículo
            _vehicleCounter++;
            Brush vehicleColor = GetRandomColor();
            Vehicle newVehicle = new Vehicle($"V{_vehicleCounter}", startNode, endNode, path, BaseVehicleSpeed, vehicleColor);

            newVehicle.UIEllipse.MouseDown += Vehicle_MouseDown;

            newVehicle.PathUILines = new List<Line>();
            for (int i = 0; i < path.Count - 1; i++)
            {
                Node current = path[i];
                Node next = path[i + 1];

                // Buscar la arista (Edge) en el grafo que conecta estos dos nodos
                // Consideramos ambas direcciones para grafos no dirigidos
                Edge edgeInPath = _graph.Edges.FirstOrDefault(e =>
                    (e.Source == current && e.Destination == next) ||
                    (e.Source == next && e.Destination == current));

                if (edgeInPath != null && edgeInPath.UILine != null)
                {
                    newVehicle.PathUILines.Add(edgeInPath.UILine);
                }
                else
                {
                    // Para depuración, si una arista esperada no se encuentra o no tiene UILine
                    Console.WriteLine($"Peligro: Arista desde {current.Id} a {next.Id} no encontrada o UILine es nula para el camino del vehiculo {newVehicle.Id}");
                }
            }

            _vehicles.Add(newVehicle);
            // Añadir el vehículo a la UI en la posición inicial del nodo de inicio
            Canvas.SetLeft(newVehicle.UIEllipse, startNode.X - newVehicle.UIEllipse.Width / 2);
            Canvas.SetTop(newVehicle.UIEllipse, startNode.Y - newVehicle.UIEllipse.Height / 2);
            GraphCanvas.Children.Add(newVehicle.UIEllipse);
        }
        private void Vehicle_MouseDown(object sender, MouseButtonEventArgs e)
        {
            Ellipse clickedEllipse = sender as Ellipse;
            if (clickedEllipse != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Vehicle clickedVehicle = _vehicles.FirstOrDefault(v => v.UIEllipse == clickedEllipse);

                if (clickedVehicle != null)
                {
                    // Si ya hay un vehículo seleccionado, desresaltar su ruta
                    if (_selectedVehicle != null)
                    {
                        _selectedVehicle.IsSelected = false;
                        HighlightVehiclePath(_selectedVehicle, false); // Desresaltar
                    }

                    // Seleccionar el nuevo vehículo
                    _selectedVehicle = clickedVehicle;
                    _selectedVehicle.IsSelected = true;
                    HighlightVehiclePath(_selectedVehicle, true); // Resaltar

                    clickedEllipse.Stroke = Brushes.Cyan; // Color diferente para el borde cuando está seleccionado
                    clickedEllipse.StrokeThickness = 2;  // Un poco más grueso para destacar
                }
                e.Handled = true; // Indicar que el evento ha sido manejado
            }
        }

        private void HighlightVehiclePath(Vehicle vehicle, bool highlight)
        {
            if (vehicle == null || vehicle.PathUILines == null) return;

            foreach (Line line in vehicle.PathUILines)
            {
                if (highlight)
                {
                    line.Stroke = SelectedPathColor;
                    line.StrokeThickness = SelectedPathThickness;
                    Canvas.SetZIndex(line, 2); // Líneas normales pueden ser 0 o 1, nodos 3, vehículos 4
                }
                else
                {
                    Edge originalEdge = _graph.Edges.FirstOrDefault(e => e.UILine == line);
                    if (originalEdge != null)
                    {
                        line.Stroke = Brushes.Black; // Color original de la arista
                        line.StrokeThickness = 2;    // Grosor original
                    }
                    Canvas.SetZIndex(line, 0); // Volver a su ZIndex normal
                }
            }

            // Si se deselecciona, restaurar también el borde del vehículo
            if (!highlight && vehicle.UIEllipse != null)
            {
                vehicle.UIEllipse.Stroke = Brushes.Black;
                vehicle.UIEllipse.StrokeThickness = 1;
            }
        }

        private void RecalculateAllVehiclePaths()
        {
            // Detener la simulación brevemente si está activa para evitar problemas durante el recálculo
            bool simulationWasRunning = false;
            if (_simulationTimer.IsEnabled)
            {
                _simulationTimer.Stop();
                simulationWasRunning = true;
            }

            // Lista temporal para vehículos que podrían necesitar ser removidos si no se encuentra ruta
            List<Vehicle> vehiclesToRemove = new List<Vehicle>();

            foreach (var vehicle in _vehicles)
            {
                // Desresaltar la ruta vieja si este vehículo estaba seleccionado antes de recalcular
                if (vehicle == _selectedVehicle)
                {
                    HighlightVehiclePath(vehicle, false);
                }

                Graph.DijkstraResult result = _graph.CalculateShortestPaths(vehicle.StartNode); // Siempre desde el StartNode original
                List<Node> newPath = _graph.GetShortestPath(vehicle.StartNode, vehicle.EndNode, result);

                if (newPath != null && newPath.Count > 1)
                {
                    vehicle.Path = newPath;
                    vehicle.CurrentPathSegmentIndex = 0; // Reinicia el vehículo al inicio de su nueva ruta
                    vehicle.DistanceAlongSegment = 0;

                    // Actualizar también las referencias a las UILines en el vehículo
                    vehicle.PathUILines.Clear();
                    for (int i = 0; i < newPath.Count - 1; i++)
                    {
                        Node current = newPath[i];
                        Node next = newPath[i + 1];
                        Edge edgeInNewPath = _graph.Edges.FirstOrDefault(e =>
                            (e.Source == current && e.Destination == next) ||
                            (e.Source == next && e.Destination == current));

                        if (edgeInNewPath != null && edgeInNewPath.UILine != null)
                        {
                            vehicle.PathUILines.Add(edgeInNewPath.UILine);
                        }
                    }

                    // Si este vehículo estaba seleccionado, resaltar su nueva ruta
                    if (vehicle == _selectedVehicle)
                    {
                        HighlightVehiclePath(vehicle, true);
                    }

                }
                else
                {
                    // Si no se encuentra una ruta (ej. por un cambio que bloqueó el camino),
                    // el vehículo ya no tiene un camino válido y debería ser removido.
                    if (vehicle == _selectedVehicle)
                    {
                        HighlightVehiclePath(vehicle, false); // Desresaltar si estaba seleccionado
                        _selectedVehicle = null; // Deseleccionar
                    }
                    vehiclesToRemove.Add(vehicle);
                    MessageBox.Show($"No se ha encontrado camino para el vehiculo {vehicle.Id} desde {vehicle.StartNode.Id} a {vehicle.EndNode.Id} después del cambio de peso de la arista. Removiendo vehiculo.", "Error de ruta", MessageBoxButton.OK, MessageBoxImage.Warning);
                }
            }

            // Remover vehículos que ya no tienen ruta
            foreach (var vehicle in vehiclesToRemove)
            {
                _vehicles.Remove(vehicle);
                GraphCanvas.Children.Remove(vehicle.UIEllipse); // Remover de la UI
            }


            // Reanudar la simulación si estaba activa
            if (simulationWasRunning)
            {
                _simulationTimer.Start();
            }
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
                    return;
                }
                // Verificar si el nombre ya existe
                if (_graph.Nodes.Any(n => n.Id.Equals(nodeId, StringComparison.OrdinalIgnoreCase)))
                {
                    MessageBox.Show($"Nodo con el nombre '{nodeId}' ya existe. Porfavor seleccione un nombre diferente.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
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
                    deleteItem.Click += DeleteEdge_Click; // Asignar el evento para borrar arista
                    cm.Items.Add(deleteItem);

                    MenuItem changeWeightItem = new MenuItem { Header = "Cambiar peso" };
                    changeWeightItem.Click += ChangeEdgeWeight_Click; // Asignar el evento para cambiar peso
                    cm.Items.Add(changeWeightItem);

                    clickedLine.ContextMenu = cm; // Asignar el ContextMenu a la línea
                    cm.IsOpen = true; // Abrir el menú contextual
                    e.Handled = true; // Indicar que el evento ha sido manejado
                }
            }
        }
        private void ChangeEdgeWeight_Click(object sender, RoutedEventArgs e)
        {
            // Este método se llama cuando el usuario hace clic en "Cambiar peso"
            if (_contextMenuEdge != null)
            {
                string newWeightStr = Microsoft.VisualBasic.Interaction.InputBox(
                    $"Peso actual: {_contextMenuEdge.Weight}. Introduzca un nuevo peso:",
                    "Cambiar peso de arista",
                    _contextMenuEdge.Weight.ToString("F0")); // Mostrar el peso actual como valor predeterminado

                if (double.TryParse(newWeightStr, out double newWeight) && newWeight > 0)
                {
                    // 1. Actualizar el peso en el modelo de la arista principal
                    _contextMenuEdge.Weight = newWeight;
                    if (_contextMenuEdge.WeightTextBlock != null)
                    {
                        _contextMenuEdge.WeightTextBlock.Text = newWeight.ToString("F0"); // Actualizar el texto del peso en la UI
                    }

                    // 2. Buscar y actualizar la arista inversa (si el grafo es bidireccional)
                    // Esto es crucial para mantener la consistencia si tienes aristas en ambas direcciones
                    Edge reverseEdge = _graph.Edges.FirstOrDefault(ed =>
                        ed.Source == _contextMenuEdge.Destination && ed.Destination == _contextMenuEdge.Source);
                    if (reverseEdge != null)
                    {
                        reverseEdge.Weight = newWeight; // Asigna el mismo peso a la inversa
                                                        // Actualizar también el TextBlock de la arista inversa si lo tienes
                        if (reverseEdge.WeightTextBlock != null)
                        {
                            reverseEdge.WeightTextBlock.Text = newWeight.ToString("F0");
                        }
                    }

                    // 3. ¡IMPORTANTE! Recalcular las rutas para todos los vehículos.
                    // Esto asegura que los vehículos consideren el nuevo peso en sus caminos más cortos.
                    // Esto cumple con el Requerimiento 004: "visualizar los cambios en los cálculos de rutas más cortas".
                    RecalculateAllVehiclePaths(); // Agregado

                    MessageBox.Show($"El peso de la arista se ha actualizado a {newWeight}.", "Peso Actualizado", MessageBoxButton.OK, MessageBoxImage.Information);
                }
                else
                {
                    MessageBox.Show("Peso inválido. Debe ser un número positivo.", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
                }
            }
            // Siempre limpia la referencia a la arista del menú contextual después de usarla
            _contextMenuEdge = null;
        }

        private void DeleteEdge_Click(object sender, RoutedEventArgs e)
        {
            // Este método se llama cuando el usuario hace clic en "Borrar arista"
            if (_contextMenuEdge != null)
            {
                // Confirmación (opcional, pero buena práctica)
                MessageBoxResult result = MessageBox.Show(
                    $"¿Está seguro que quiere borrar la arista entre {_contextMenuEdge.Source.Id} y {_contextMenuEdge.Destination.Id}?",
                    "Confirmar eliminación",
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
            // Crear el contenedor principal para el nodo (Grid)
            // El Grid será el padre de la forma (Ellipse) y la etiqueta (TextBlock).
            Grid nodeContainer = new Grid();
            nodeContainer.Width = 60; // Ajusta el tamaño del contenedor al tamaño del nodo visual
            nodeContainer.Height = 60;

            // Dibujar un círculo para el nodo (nodeShape)
            Ellipse nodeShape = new Ellipse
            {
                Width = 60,
                Height = 60,
                Fill = Brushes.Blue,
                Stroke = Brushes.DarkBlue,
                StrokeThickness = 2
            };

            // Agregar la forma al contenedor del nodo (nodeContainer es ahora su padre)
            nodeContainer.Children.Add(nodeShape); // ¡Aquí se añade primero!

            // Almacenar referencia al UIElement en el modelo Node
            node.UIEllipse = nodeShape; // La referencia a la elipse individual sigue siendo útil

            // Agregar una etiqueta de texto para el nombre del nodo
            TextBlock nodeLabel = new TextBlock
            {
                Text = node.Id,
                Foreground = Brushes.White,
                HorizontalAlignment = HorizontalAlignment.Center, // Centrado dentro del Grid
                VerticalAlignment = VerticalAlignment.Center,   // Centrado dentro del Grid
                FontSize = 10,
                IsHitTestVisible = false // Para que los clics pasen al Ellipse de abajo
            };

            // Agregar la etiqueta al contenedor del nodo (nodeContainer es ahora su padre)
            nodeContainer.Children.Add(nodeLabel); // ¡Aquí se añade primero!

            // Almacenar referencia a la etiqueta individual en el modelo Node
            node.UILabel = nodeLabel; 

            // Asignar el contenedor completo al nodo en tu modelo
            node.UIContainer = nodeContainer; //

            // Centrar el CONTENEDOR del nodo en las coordenadas de clic en el Canvas
            Canvas.SetLeft(node.UIContainer, node.X - nodeContainer.Width / 2);
            Canvas.SetTop(node.UIContainer, node.Y - nodeContainer.Height / 2);
            Canvas.SetZIndex(node.UIContainer, 2); //

            // Manejar eventos de clic en el CONTENEDOR del nodo
            // Esto es importante porque el Grid ahora contiene Ellipse y TextBlock,
            // y quieres que el Grid completo responda a los eventos del ratón.
            node.UIContainer.MouseLeftButtonDown += Node_MouseLeftButtonDown; //
            node.UIContainer.MouseMove += Node_MouseMove; //
            node.UIContainer.MouseLeftButtonUp += Node_MouseLeftButtonUp;
            node.UIContainer.MouseRightButtonUp += Node_MouseRightButtonUp;


            // Finalmente, agregar el CONTENEDOR del nodo al Canvas principal
            GraphCanvas.Children.Add(node.UIContainer);
        }

        public void Node_MouseDown(object sender, MouseButtonEventArgs e)
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

        public void Node_MouseUp(object sender, MouseButtonEventArgs e)
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

            Canvas.SetZIndex(edgeLine, 0);
            Canvas.SetZIndex(weightText, 1);

            GraphCanvas.Children.Insert(0, edgeLine);
            GraphCanvas.Children.Add(weightText);

            edge.UILine = edgeLine; 
            edge.WeightTextBlock = weightText;
        }
        private void TryConnectNodes(Node sourceNode, Node destinationNode)
        {
            // Verificar si los nodos son los mismos o si la arista ya existe
            bool edgeExists = _graph.Edges.Any(ed =>
                (ed.Source == sourceNode && ed.Destination == destinationNode) ||
                (ed.Source == destinationNode && ed.Destination == sourceNode));

            if (edgeExists)
            {
                MessageBox.Show("Una arista ya existe entre estos dos nodos.", "Información", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            // Pedir el peso de la arista
            string weightStr = Microsoft.VisualBasic.Interaction.InputBox("Introduzca un peso para la conexión (ej. distancia o tiempo):", "Peso de arista", "100"); //
            if (double.TryParse(weightStr, out double weight))
            {
                Edge newEdge = new Edge(sourceNode, destinationNode, weight);
                _graph.AddEdge(newEdge); // Asumiendo que AddEdge añade la arista al grafo
                DrawEdge(newEdge);       // Asumiendo que DrawEdge dibuja la arista en el Canvas
                                         // RecalculateAllVehiclePaths(); // Descomenta si tienes este método y lo necesitas aquí
            }
            else
            {
                MessageBox.Show("Peso inválido. Se usará 100 por defecto.", "Advertencia", MessageBoxButton.OK, MessageBoxImage.Warning);
                // Opcional: si la entrada es inválida, puedes no crear la arista o crearla con un valor por defecto.
                // Aquí se crea con 100 por defecto.
                weight = 100;
                Edge newEdge = new Edge(sourceNode, destinationNode, weight);
                _graph.AddEdge(newEdge);
                DrawEdge(newEdge);
                // RecalculateAllVehiclePaths();
            }
        }

        private void Node_MouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            _selectedNodeContainer = sender as Grid; //
            if (_selectedNodeContainer != null)
            {
                // Encontrar el nodo del modelo asociado a este UIContainer
                Node clickedNode = _graph.Nodes.FirstOrDefault(n => n.UIContainer == _selectedNodeContainer);
                if (clickedNode != null)
                {
                    if (!(Keyboard.IsKeyDown(Key.LeftShift) || Keyboard.IsKeyDown(Key.RightShift)))
                    {
                        if (_selectedNodeForConnection != null && _selectedNodeForConnection != clickedNode)
                        {
                            Node destinationNode = clickedNode;

                            // Llamar al método para intentar conectar los nodos
                            TryConnectNodes(_selectedNodeForConnection, destinationNode);

                            // Resetear la selección del primer nodo (desresaltarlo)
                            _selectedNodeForConnection.UIEllipse.Stroke = Brushes.Blue; // Restaura color original
                            _selectedNodeForConnection.UIEllipse.StrokeThickness = 2; // Restaura grosor original
                            _selectedNodeForConnection = null; // Limpiar la referencia
                        }
                        else // Este es el primer nodo seleccionado para conexión
                        {
                            // Si ya teníamos un nodo seleccionado, lo desresaltamos primero
                            if (_selectedNodeForConnection != null)
                            {
                                _selectedNodeForConnection.UIEllipse.Stroke = Brushes.Blue;
                                _selectedNodeForConnection.UIEllipse.StrokeThickness = 2;
                            }

                            _selectedNodeForConnection = clickedNode; // Guarda este nodo como el primero para conexión
                            _selectedNodeForConnection.UIEllipse.Stroke = Brushes.Red; // Resaltar visualmente
                            _selectedNodeForConnection.UIEllipse.StrokeThickness = 4; // Resaltar visualmente
                        }
                    }
                    else 
                    {
                        if (_selectedNodeForConnection != null)
                        {
                            _selectedNodeForConnection.UIEllipse.Stroke = Brushes.Blue;
                            _selectedNodeForConnection.UIEllipse.StrokeThickness = 2;
                            _selectedNodeForConnection = null;
                        }

                        _isDraggingNode = true; //
                        _lastMousePosition = e.GetPosition(GraphCanvas); 
                        _selectedNodeContainer.CaptureMouse(); // Capturar el mouse para seguir el arrastre
                    }
                    e.Handled = true; // Marca el evento como manejado
                }
            }
        }

        private void Node_MouseMove(object sender, MouseEventArgs e)
        {
            if (_isDraggingNode && _selectedNodeContainer != null && e.LeftButton == MouseButtonState.Pressed)
            {
                Point currentMousePosition = e.GetPosition(GraphCanvas);
                double deltaX = currentMousePosition.X - _lastMousePosition.X;
                double deltaY = currentMousePosition.Y - _lastMousePosition.Y;

                // Actualizar la posición del nodo en la UI
                Canvas.SetLeft(_selectedNodeContainer, Canvas.GetLeft(_selectedNodeContainer) + deltaX);
                Canvas.SetTop(_selectedNodeContainer, Canvas.GetTop(_selectedNodeContainer) + deltaY);

                // Actualizar la posición del nodo en el modelo
                Node draggedNode = _graph.Nodes.FirstOrDefault(n => n.UIContainer == _selectedNodeContainer);
                if (draggedNode != null)
                {
                    draggedNode.X = Canvas.GetLeft(_selectedNodeContainer) + _selectedNodeContainer.ActualWidth / 2;
                    draggedNode.Y = Canvas.GetTop(_selectedNodeContainer) + _selectedNodeContainer.ActualHeight / 2;

                    UpdateConnectedEdges(draggedNode); // Actualizar las aristas conectadas
                }

                _lastMousePosition = currentMousePosition;
                e.Handled = true;
            }
        }

        private void Node_MouseLeftButtonUp(object sender, MouseButtonEventArgs e)
        {
            if (_isDraggingNode)
            {
                _isDraggingNode = false;
                if (_selectedNodeContainer != null)
                {
                    _selectedNodeContainer.ReleaseMouseCapture();
                    _selectedNodeContainer = null;
                }
                RecalculateAllVehiclePaths(); // Recalcular rutas después de soltar el nodo
                e.Handled = true;
            }
            else if (_selectedNodeForConnection != null)
            {
                // Lógica para finalizar la conexión si se soltó sobre otro nodo
                Grid releasedContainer = sender as Grid;
                if (releasedContainer != null)
                {
                    Node destinationNode = _graph.Nodes.FirstOrDefault(n => n.UIContainer == releasedContainer);

                    if (destinationNode != null && destinationNode != _selectedNodeForConnection)
                    {
                        bool edgeExists = _graph.Edges.Any(ed =>
                            (ed.Source == _selectedNodeForConnection && ed.Destination == destinationNode) ||
                            (ed.Source == destinationNode && ed.Destination == _selectedNodeForConnection));

                        if (!edgeExists)
                        {
                            string weightStr = Interaction.InputBox("Introduzca un peso (ej., distancia o tiempo):", "Peso de arista", "100");
                            if (!double.TryParse(weightStr, out double weight) || weight <= 0)
                            {
                                MessageBox.Show("Peso invalido. Usando predeterminado de 100.", "Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                                weight = 100;
                            }

                            Edge newEdge = new Edge(_selectedNodeForConnection, destinationNode, weight);
                            _graph.AddEdge(newEdge);
                            DrawEdge(newEdge);
                            RecalculateAllVehiclePaths(); // Recalcular rutas después de añadir una nueva arista
                        }
                        else
                        {
                            MessageBox.Show("Una arista ya existe entre estos dos nodos.", "Info", MessageBoxButton.OK, MessageBoxImage.Information);
                        }
                    }
                }

                // Deshacer el resaltado y resetear la selección de conexión
                _selectedNodeForConnection.UIEllipse.Stroke = Brushes.DarkBlue;
                _selectedNodeForConnection.UIEllipse.StrokeThickness = 2;
                _selectedNodeForConnection = null;
                e.Handled = true;
            }
        }

        private void Node_MouseRightButtonUp(object sender, MouseButtonEventArgs e)
        {
            Grid clickedNodeContainer = sender as Grid;
            if (clickedNodeContainer != null)
            {
                Node clickedNode = _graph.Nodes.FirstOrDefault(n => n.UIContainer == clickedNodeContainer);
                if (clickedNode != null)
                {
                    ContextMenu cm = new ContextMenu();
                    MenuItem deleteNodeItem = new MenuItem { Header = "Borrar Nodo" };
                    deleteNodeItem.Click += (s, ev) => DeleteNode_Click(clickedNode);
                    cm.Items.Add(deleteNodeItem);

                    clickedNodeContainer.ContextMenu = cm;
                    cm.IsOpen = true;
                    e.Handled = true;
                }
            }
        }

        private void DeleteNode_Click(Node nodeToDelete)
        {
            MessageBoxResult result = MessageBox.Show(
                $"¿Está seguro de que desea eliminar el nodo '{nodeToDelete.Id}' y todas sus aristas conectadas?",
                "Confirmar Eliminación",
                MessageBoxButton.YesNo,
                MessageBoxImage.Warning);

            if (result == MessageBoxResult.Yes)
            {
                // Eliminar el nodo de la UI
                if (nodeToDelete.UIContainer != null)
                {
                    GraphCanvas.Children.Remove(nodeToDelete.UIContainer);
                }

                // Eliminar aristas conectadas al nodo (debe hacerse antes de eliminar el nodo del modelo)
                List<Edge> edgesToRemove = _graph.Edges
                    .Where(e => e.Source == nodeToDelete || e.Destination == nodeToDelete)
                    .ToList();

                foreach (var edge in edgesToRemove)
                {
                    GraphCanvas.Children.Remove(edge.UILine);
                    if (edge.WeightTextBlock != null)
                    {
                        GraphCanvas.Children.Remove(edge.WeightTextBlock);
                    }
                    _graph.Edges.Remove(edge); // Eliminar del modelo después de la UI
                }

                // Eliminar el nodo del modelo
                _graph.Nodes.Remove(nodeToDelete);

                // Deseleccionar el nodo si era el seleccionado para conexión
                if (_selectedNodeForConnection == nodeToDelete)
                {
                    _selectedNodeForConnection = null;
                }

                // Si hay un vehículo seleccionado que dependa de este nodo o pasa por él, deseleccionarlo y recalcular
                List<Vehicle> vehiclesToRecalculate = _vehicles.Where(v =>
                    v.StartNode == nodeToDelete || v.EndNode == nodeToDelete || v.Path.Contains(nodeToDelete)
                ).ToList();

                foreach (var vehicle in vehiclesToRecalculate)
                {
                    if (vehicle == _selectedVehicle)
                    {
                        HighlightVehiclePath(vehicle, false); // Desresaltar la ruta vieja
                        _selectedVehicle = null;
                    }
                    // El RecalculateAllVehiclePaths manejará la remoción si no hay ruta
                }

                RecalculateAllVehiclePaths(); // Recalcular todas las rutas de vehículos
            }
        }
        private void UpdateConnectedEdges(Node movedNode)
        {
            foreach (var edge in _graph.Edges)
            {
                if (edge.Source == movedNode)
                {
                    edge.UILine.X1 = movedNode.X;
                    edge.UILine.Y1 = movedNode.Y;
                    UpdateEdgeWeightTextPosition(edge);
                }
                else if (edge.Destination == movedNode)
                {
                    edge.UILine.X2 = movedNode.X;
                    edge.UILine.Y2 = movedNode.Y;
                    UpdateEdgeWeightTextPosition(edge);
                }
            }
        }
        private void UpdateEdgeWeightTextPosition(Edge edge)
        {
            if (edge.WeightTextBlock != null)
            {
                double centerX = (edge.Source.X + edge.Destination.X) / 2;
                double centerY = (edge.Source.Y + edge.Destination.Y) / 2;

                Canvas.SetLeft(edge.WeightTextBlock, centerX - (edge.WeightTextBlock.ActualWidth / 2));
                Canvas.SetTop(edge.WeightTextBlock, centerY - (edge.WeightTextBlock.ActualHeight / 2) - 10);
            }
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

        private void GraphCanvas_MouseMove(object sender, MouseEventArgs e)
        {
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

