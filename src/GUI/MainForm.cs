using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using Microsoft.Win32;


namespace Switchie
{
    public class MainForm : Form
    {
        private string version = "v1.1.8";
        private Point dragOffset;
        private bool _isAppPinned = false;
        private int _activeDesktopIndex = 0;
        private bool _forceAlwaysOnTop = false;
        private string _windowsHash = string.Empty;
        private List<VirtualDesktop> _virtualDesktops = new List<VirtualDesktop>();
        
        private Boolean top = false;      // top position of bar

        public int BorderSize { get; set; } = 1;
        public int PagerHeight { get; set; } = 40;
        public bool IsDraggingWindow { get; set; }
        public int VirtualDesktopSpacing { get; set; } = 4;
        public Color DesktopColor { get; set; } = Color.FromArgb(64, 64, 64);
        public Color WindowColor { get; set; } = Color.Gray;
        public Color WindowBorderColor { get; set; } = Color.Silver;
        public Color ActiveWindowColor { get; set; } = Color.Silver;
        public Color ActiveWindowBorderColor { get; set; } = Color.White;
        public Color ActiveDesktopBorderColor { get; set; } = Color.White;
        public ConcurrentBag<Window> Windows = new ConcurrentBag<Window>();

        private Color LoadColorFromRegistry(string keyName)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Switchie");
                if (key != null)
                {
                    string colorString = (string)key.GetValue(keyName, "#FFFFFF");
                    key.Close();
                    return ColorTranslator.FromHtml(colorString);
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Error loading color from registry: {ex.Message}");
            }

            return System.Drawing.Color.FromArgb(((int)(((byte)(64)))), ((int)(((byte)(64)))), ((int)(((byte)(64))))); // Default color if loading fails
        }

        private void SaveColorToRegistry(string keyName, Color color)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Switchie", true);
                key.SetValue(keyName, ColorTranslator.ToHtml(color));
                key.Close();
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Error saving color to registry: {ex.Message}");
            }
        }

        private void SaveBooleanToRegistry(string keyName, bool value)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Switchie", true);
                key.SetValue(keyName, value ? 1 : 0);
                key.Close();
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Error saving boolean to registry: {ex.Message}");
            }
        }

        private bool ReadBooleanFromRegistry(string keyName)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Switchie");
                if (key != null)
                {
                    int value = (int)key.GetValue(keyName, 0);
                    key.Close();
                    return value == 1;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Error saving boolean to registry: {ex.Message}");
            }    
            return false;
        }        

        private void SaveIntToRegistry(string keyName, int value)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.CreateSubKey(@"Software\Switchie", true);
                key.SetValue(keyName, value);
                key.Close();
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Error saving int to registry: {ex.Message}");
            }
        }

        private int ReadIntFromRegistry(string keyName, int missing)
        {
            try
            {
                RegistryKey key = Registry.CurrentUser.OpenSubKey(@"Software\Switchie");
                if (key != null)
                {
                    int value = (int)key.GetValue(keyName, missing);
                    key.Close();
                    return value;
                }
            }
            catch (UnauthorizedAccessException ex)
            {
                MessageBox.Show($"Error saving int to registry: {ex.Message}");
            }    
            return missing;
        }               
        private void SetPosition()
        {            
            if (top) Location = new System.Drawing.Point((Screen.PrimaryScreen.Bounds.Width / 2) - (Size.Width / 2), Screen.PrimaryScreen.WorkingArea.Top);
            else Location = new System.Drawing.Point((Screen.PrimaryScreen.Bounds.Width / 2) - (Size.Width / 2), Screen.PrimaryScreen.WorkingArea.Bottom - Size.Height);
        }      

        public MainForm()
        {
            SuspendLayout();
            DoubleBuffered = true;
            AutoScaleDimensions = new System.Drawing.SizeF(6F, 13F);
            AutoScaleMode = System.Windows.Forms.AutoScaleMode.Font;
            BackColor = LoadColorFromRegistry("SelectedColor");
            ClientSize = new System.Drawing.Size(1, 1);
            ControlBox = false;
            AllowDrop = true;
            MinimumSize = new System.Drawing.Size(1, 1);
            StartPosition = FormStartPosition.Manual;
            FormBorderStyle = System.Windows.Forms.FormBorderStyle.None;
            MaximizeBox = false;
            MinimizeBox = false;
            Name = "frmMain";
            TopMost = true;
            Icon = new System.Drawing.Icon(new MemoryStream(Helpers.GetResourceFromAssembly(typeof(Program), "Switchie.Resources.icon.ico")));    

            PagerHeight = ReadIntFromRegistry("Height", PagerHeight);

            Enumerable.Range(0, WindowsVirtualDesktop.GetInstance().Count).ToList().ForEach(x =>
            {
                VirtualDesktop desktop = new VirtualDesktop(x, this, new Point(_virtualDesktops.Sum(y => y.Size.Width), 0));
                MouseUp += desktop.OnMouseUp;
                MouseDown += desktop.OnMouseDown;
                MouseMove += desktop.OnMouseMove;
                DragOver += desktop.OnDragOver;
                DragDrop += desktop.OnDragDrop;
                _virtualDesktops.Add(desktop);
            });
            Size = new Size(_virtualDesktops.Sum(x => x.Size.Width), PagerHeight);
            MinimumSize = Size;
            MaximumSize = Size;
            ClientSize = Size;
 
            top = ReadBooleanFromRegistry("TopBottom");
            SetPosition();  // set the bar position on screen
            
            ResumeLayout(false);
            Shown += OnShown;
            MouseUp += OnMouseUp;
            MouseDown += OnMouseDown;
            MouseMove += OnMouseMove;
        }

        private void OnShown(object sender, EventArgs e)
        {
            Task.Run(async () =>
            {
                while (!Program.ApplicationClosing.IsCancellationRequested)
                {
                    Invoke(new Action(() =>
                    {
                        try
                        {
                            if (_forceAlwaysOnTop)
                                WindowManager.SetAlwaysOnTop(Handle, _forceAlwaysOnTop);
                            Windows = new ConcurrentBag<Window>(WindowManager.GetOpenWindows());
                            var hash = $"{_activeDesktopIndex}{Windows.Sum(x => Math.Abs(x.Dimensions.X))}{Windows.Sum(x => Math.Abs(x.Dimensions.Y))}{Windows.Sum(x => x.Dimensions.Width)}{Windows.Sum(x => x.Dimensions.Height)}{string.Join("", Windows.Select(x => x.IsActive ? 1 : 0))}{string.Join("", Windows.Select(x => x.VirtualDesktopIndex))}";
                            if (hash != _windowsHash)
                            {
                                _windowsHash = hash;
                                Invalidate();
                            }
                        }
                        catch { }
                    }));
                    await Task.Delay(1);
                }
            });
            Task.Run(async () =>
            {
                while (!Program.ApplicationClosing.IsCancellationRequested)
                {
                    Invoke(new Action(() =>
                    {
                        if (!_isAppPinned)
                        {
                            try
                            {
                                WindowsVirtualDesktopManager.GetInstance().PinApplication(Handle);
                                _isAppPinned = true;
                            }
                            catch { }
                        }
                        try
                        {
                            _activeDesktopIndex = WindowsVirtualDesktopManager.GetInstance().FromDesktop(WindowsVirtualDesktop.GetInstance().Current);
                            Windows = new ConcurrentBag<Window>(WindowManager.GetOpenWindows());
                            Invalidate();
                        }
                        catch { }
                    }));
                    await Task.Delay(50);
                }
            });
        }

        private void OnMouseUp(object sender, MouseEventArgs e)
        {
            IsDraggingWindow = false;
            Cursor = Cursors.Default;
            if ((e.Button & MouseButtons.Right) == MouseButtons.Right)
            {
                _forceAlwaysOnTop = false;
                ContextMenuStrip menu = new ContextMenuStrip();
                string winver= "WinVer="+Program.WindowsVersion.Major+":"+Program.WindowsVersion.Minor+":"+Program.WindowsVersion.Build+" "+Program.WindowsVersion.Name;
                Helpers.AddMenuItem(this, menu, new ToolStripMenuItem() 
                  { Text = "About" }, () => 
                  { MessageBox.Show($"Switchie{Environment.NewLine}"+
                                    version+$"{Environment.NewLine}{Environment.NewLine}"+
                                    $"Made by darkguy2008{Environment.NewLine}"+
                                    $"Modify by ice00{Environment.NewLine}{Environment.NewLine}"+
                                    winver,
                   "About"); _forceAlwaysOnTop = true; }
                );

                menu.Items.Add(new ToolStripSeparator());

                Helpers.AddMenuItem(this, menu, new ToolStripMenuItem() 
                    { Text = "Top position" }, () => 
                    { 
                        top = true;
                        SetPosition(); 
                        SaveBooleanToRegistry("TopBottom", top);
                    }
                );

                Helpers.AddMenuItem(this, menu, new ToolStripMenuItem() 
                    {   Text = "Bottom position" }, () => 
                    { 
                        top = false;
                        SetPosition();
                        SaveBooleanToRegistry("TopBottom", top);
                    }
                );             

                Helpers.AddMenuItem(this, menu, new ToolStripMenuItem() 
                { Text = "Windows color" }, () => 
                { using (ColorDialog colorDialog = new ColorDialog()) {
                    if (colorDialog.ShowDialog() == DialogResult.OK)
                    {
                        Color selectedColor = colorDialog.Color;
                        // save the choice in registry
                        SaveColorToRegistry("SelectedColor", selectedColor);
                        BackColor = selectedColor;
                    }
                  }
                });             

                Helpers.AddMenuItem(this, menu, new ToolStripMenuItem() 
                {  Text = "Dimension" }, () => 
                {  
                    Form dialog = new Form();
                    dialog.Text = "Dimension";
                    dialog.StartPosition = FormStartPosition.CenterScreen; 
                    dialog.FormBorderStyle = FormBorderStyle.FixedDialog; 
                    dialog.MaximizeBox = false; 
                    dialog.MinimizeBox = false; 
                    dialog.AutoSize = true; 
                    dialog.AutoSizeMode = AutoSizeMode.GrowAndShrink; 

                    Label label = new Label();
                    label.Text = "Select pager height:";
                    label.Location = new Point(10, 10);
                    label.AutoSize = true;

                    NumericUpDown numericUpDown = new NumericUpDown();
                    numericUpDown.Minimum = 25;
                    numericUpDown.Maximum = 60;
                    numericUpDown.Value = PagerHeight; // inital value
                    numericUpDown.Location = new Point(10, 40);

                    numericUpDown.ValueChanged += (sender_, e_) => 
                    {
                        try
                        {
                            if (numericUpDown.Value < numericUpDown.Minimum || numericUpDown.Value > numericUpDown.Maximum)
                            {
                                throw new ArgumentOutOfRangeException();
                            }
                        }
                        catch (ArgumentOutOfRangeException)
                        {
                            numericUpDown.Value = numericUpDown.Minimum;
                        }
                    };                    
                    
                    Button confirmButton = new Button();
                    confirmButton.Text = "OK";
                    confirmButton.Location = new Point(10, 70);
                    confirmButton.Click += (buttonSender, buttonEventArgs) => 
                    {                        
                        int selectedValue = (int)numericUpDown.Value;
                        PagerHeight = selectedValue;

                        SaveIntToRegistry("Height", PagerHeight);
                        
                        dialog.Close();

                        _virtualDesktops.Clear();

                        Enumerable.Range(0, WindowsVirtualDesktop.GetInstance().Count).ToList().ForEach(x =>
                        {
                            VirtualDesktop desktop = new VirtualDesktop(x, this, new Point(_virtualDesktops.Sum(y => y.Size.Width), 0));
                            MouseUp += desktop.OnMouseUp;
                            MouseDown += desktop.OnMouseDown;
                            MouseMove += desktop.OnMouseMove;
                            DragOver += desktop.OnDragOver;
                            DragDrop += desktop.OnDragDrop;
                            _virtualDesktops.Add(desktop);
                        });
                            
                        Size tmpSize =  new Size(_virtualDesktops.Sum(x => x.Size.Width), PagerHeight);     
                        MinimumSize = tmpSize;
                        MaximumSize = tmpSize;
                        ClientSize = tmpSize;
                        Size = tmpSize;

                        top = ReadBooleanFromRegistry("TopBottom");
                        SetPosition();  // set the bar position on screen

                        Invalidate();
                        Update();
                    };

                    dialog.Controls.Add(label);
                    dialog.Controls.Add(numericUpDown);
                    dialog.Controls.Add(confirmButton);

                    dialog.ShowDialog();
                });
    
                menu.Items.Add(new ToolStripSeparator());   

                Helpers.AddMenuItem(this, menu, new ToolStripMenuItem() { 
                    Text = "Exit" }, () => 
                    { Environment.Exit(1); }
                );                
                menu.Opened += (ss, ee) => _forceAlwaysOnTop = false;
                menu.Show(this, PointToClient(Cursor.Position));
            }
        }

        private void OnMouseDown(object sender, MouseEventArgs e)
        {
            if ((e.Button & MouseButtons.Middle) == MouseButtons.Middle)
            {
                IsDraggingWindow = true;
                dragOffset = e.Location;
            }
        }

        private void OnMouseMove(object sender, MouseEventArgs e)
        {
            if (IsDraggingWindow)
            {
                Cursor = Cursors.SizeAll;
                Location = new Point(e.X + Location.X - dragOffset.X, e.Y + Location.Y - dragOffset.Y);
            }
        }

        protected override void OnPaint(PaintEventArgs e)
        {
            base.OnPaint(e);
            try { _virtualDesktops.ForEach(x => x.OnPaint(e)); }
            catch
            {
                WindowsVirtualDesktop.Restart();
                WindowsVirtualDesktopManager.Restart();
            }
        }
    }
}
