using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
using System.Threading.Tasks;
using Newtonsoft.Json;
using System.Media;

namespace FeedNiko
{
    public class FoodItem
    {
        [JsonProperty("Flavor level")]
        public string FlavorLevel { get; set; } = "0/10";

        [JsonProperty("Drink")]
        public bool Drink { get; set; }

        [JsonProperty("Points for eating")]
        public int Points { get; set; }
    }

    public class PlayerData
    {
        public int TotalPoints { get; set; }
    }

    public class MainForm : Form
    {
        // Основные элементы управления
        private PictureBox nikoPicture;
        private ComboBox foodComboBox;
        private Button feedButton;
        private Button infoButton;
        private Label pointsLabel;

        // Изображения
        private Image originalImage;
        private Image eatImage;
        private Image eatBadImage;
        private Image eatVeryBadImage;

        // Звуки
        private SoundPlayer drinkSound;
        private SoundPlayer eatingSound;

        // Данные
        private Dictionary<string, FoodItem> foodItems = new Dictionary<string, FoodItem>();
        private bool isAnimating = false;
        private PlayerData playerData = new PlayerData();
        private readonly string userDataPath = "userdata";
        private readonly string userDataFile = "user.dat";

        // Вспомогательные элементы
        private ToolTip infoToolTip;

        public MainForm()
        {
            InitializeComponent();
            LoadPlayerData();
            UpdatePointsDisplay();
        }

        private void InitializeComponent()
        {
            // Настройка формы
            this.Text = "Накорми чем хочешь";
            this.ClientSize = new Size(220, 250);
            this.FormBorderStyle = FormBorderStyle.FixedSingle;
            this.MaximizeBox = false;

            // Установка иконки
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream("FeedNiko.Resources.icon.ico"))
                {
                    if (stream != null)
                        this.Icon = new Icon(stream);
                }
            }
            catch { }

            // Инициализация ресурсов
            InitializeResources();

            // Инициализация элементов управления
            InitializeControls();

            // Загрузка данных о еде
            LoadFoodItems();
        }

        private void InitializeResources()
        {
            try
            {
                originalImage = LoadImageFromResources("niko") ?? CreatePlaceholderImage("Niko", 100, 100);
                eatImage = LoadImageFromResources("eat") ?? CreatePlaceholderImage("Ест", 100, 100);
                eatBadImage = LoadImageFromResources("eat_bad") ?? CreatePlaceholderImage("Ест плохо", 100, 100);
                eatVeryBadImage = LoadImageFromResources("eat_very_bad") ?? CreatePlaceholderImage("Ест очень плохо", 100, 100);

                // Загрузка звуков
                var assembly = Assembly.GetExecutingAssembly();
                using (var drinkStream = assembly.GetManifestResourceStream("FeedNiko.Resources.drink.wav"))
                using (var eatStream = assembly.GetManifestResourceStream("FeedNiko.Resources.eating.wav"))
                {
                    if (drinkStream != null) drinkSound = new SoundPlayer(drinkStream);
                    if (eatStream != null) eatingSound = new SoundPlayer(eatStream);

                    // Предзагрузка звуков в память
                    if (drinkSound != null) drinkSound.Load();
                    if (eatingSound != null) eatingSound.Load();
                }
            }
            catch
            {
                originalImage = CreatePlaceholderImage("Niko", 100, 100);
                eatImage = CreatePlaceholderImage("Ест", 100, 100);
                eatBadImage = CreatePlaceholderImage("Ест плохо", 100, 100);
                eatVeryBadImage = CreatePlaceholderImage("Ест очень плохо", 100, 100);
            }
        }

        private Image LoadImageFromResources(string resourceName)
        {
            try
            {
                var assembly = Assembly.GetExecutingAssembly();
                using (var stream = assembly.GetManifestResourceStream($"FeedNiko.Resources.{resourceName}.png"))
                {
                    return stream != null ? Image.FromStream(stream) : null;
                }
            }
            catch
            {
                return null;
            }
        }

        private Image CreatePlaceholderImage(string text, int width, int height)
        {
            var image = new Bitmap(width, height);
            using (Graphics g = Graphics.FromImage(image))
            {
                g.Clear(Color.LightGray);
                g.DrawString(text, new Font("Arial", 10), Brushes.Black, 10, 10);
                g.DrawRectangle(Pens.Black, 0, 0, width - 1, height - 1);
            }
            return image;
        }

        private Image CreateInfoButtonImage()
        {
            var bmp = new Bitmap(16, 16);
            using (Graphics g = Graphics.FromImage(bmp))
            {
                g.Clear(Color.LightBlue);
                g.DrawString("i", new Font("Arial", 10, FontStyle.Bold), Brushes.Black, 3, 1);
            }
            return bmp;
        }

        private void InitializeControls()
        {
            // PictureBox для Niko
            nikoPicture = new PictureBox
            {
                Image = originalImage,
                SizeMode = PictureBoxSizeMode.Zoom,
                Size = new Size(100, 100),
                Location = new Point(60, 10),
                BorderStyle = BorderStyle.FixedSingle
            };
            this.Controls.Add(nikoPicture);

            // ComboBox для выбора еды
            foodComboBox = new ComboBox
            {
                DropDownStyle = ComboBoxStyle.DropDownList,
                Location = new Point(10, 120),
                Width = 150
            };
            foodComboBox.SelectedIndexChanged += FoodComboBox_SelectedIndexChanged;
            this.Controls.Add(foodComboBox);

            // Кнопка информации
            infoButton = new Button
            {
                Size = new Size(20, 20),
                Location = new Point(165, 120),
                FlatStyle = FlatStyle.Flat,
                BackgroundImage = LoadImageFromResources("info") ?? CreateInfoButtonImage(),
                BackgroundImageLayout = ImageLayout.Stretch
            };
            infoButton.FlatAppearance.BorderSize = 1;
            infoButton.Click += InfoButton_Click;
            this.Controls.Add(infoButton);

            // Кнопка "Накормить!"
            feedButton = new Button
            {
                Text = "Накормить!",
                Location = new Point(10, 150),
                Width = 180
            };
            feedButton.Click += FeedButton_Click;
            this.Controls.Add(feedButton);

            // Label с очками игрока (по центру внизу)
            pointsLabel = new Label
            {
                Location = new Point(10, 190),
                Width = 200,
                Font = new Font(FontFamily.GenericSansSerif, 12, FontStyle.Bold),
                TextAlign = ContentAlignment.MiddleCenter,
                Text = "0"
            };
            this.Controls.Add(pointsLabel);

            // Инициализация ToolTip
            infoToolTip = new ToolTip
            {
                AutoPopDelay = 5000,
                InitialDelay = 500,
                ReshowDelay = 500,
                ShowAlways = true
            };
        }

        private void InfoButton_Click(object sender, EventArgs e)
        {
            if (foodComboBox.SelectedItem == null)
            {
                infoToolTip.Show("Сначала выберите еду", infoButton, 3000);
                return;
            }

            string foodName = foodComboBox.SelectedItem.ToString();
            if (!foodItems.TryGetValue(foodName, out FoodItem item) || item == null)
            {
                infoToolTip.Show("Нет данных о выбранной еде", infoButton, 3000);
                return;
            }

            // Создаем форму для отображения информации
            Form infoForm = new Form
            {
                FormBorderStyle = FormBorderStyle.FixedToolWindow,
                StartPosition = FormStartPosition.Manual,
                Size = new Size(200, 150),
                Location = PointToScreen(new Point(infoButton.Left, infoButton.Bottom)),
                ShowInTaskbar = false,
                TopMost = true,
                Text = "Информация о еде"
            };

            string infoText = $"Название: {foodName}\n\n" +
                             $"Уровень вкуса: {item.FlavorLevel}\n" +
                             $"Тип: {(item.Drink ? "Напиток" : "Еда")}\n" +
                             $"Очки: {item.Points}";

            Label infoLabel = new Label
            {
                Text = infoText,
                Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleCenter,
                Font = new Font("Arial", 9)
            };

            infoForm.Controls.Add(infoLabel);
            infoForm.Show();
        }

        private void FoodComboBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            try
            {
                if (foodComboBox.SelectedItem == null)
                {
                    feedButton.Enabled = false;
                    return;
                }

                string foodName = foodComboBox.SelectedItem.ToString();
                if (string.IsNullOrEmpty(foodName) || !foodItems.ContainsKey(foodName))
                {
                    feedButton.Enabled = false;
                    return;
                }

                feedButton.Enabled = foodItems[foodName].Points <= 10;
            }
            catch
            {
                feedButton.Enabled = false;
            }
        }

        private void LoadFoodItems()
        {
            foodItems.Clear();
            foodComboBox.Items.Clear();

            try
            {
                string foodsPath = Path.Combine(Application.StartupPath, "Foods");
                if (!Directory.Exists(foodsPath))
                {
                    Directory.CreateDirectory(foodsPath);
                    return;
                }

                foreach (var folder in Directory.GetDirectories(foodsPath))
                {
                    try
                    {
                        string folderName = Path.GetFileName(folder);
                        string jsonPath = Path.Combine(folder, "data.json");
                        string imagePath = Path.Combine(folder, "image.png");

                        if (File.Exists(jsonPath) && File.Exists(imagePath))
                        {
                            string jsonContent = File.ReadAllText(jsonPath);
                            FoodItem foodItem = JsonConvert.DeserializeObject<FoodItem>(jsonContent);

                            if (foodItem != null)
                            {
                                foodItems.Add(folderName, foodItem);
                                foodComboBox.Items.Add(folderName);
                            }
                        }
                    }
                    catch { }
                }

                if (foodComboBox.Items.Count > 0)
                {
                    foodComboBox.SelectedIndex = 0;
                }
            }
            catch { }
        }

        private async void FeedButton_Click(object sender, EventArgs e)
        {
            if (isAnimating || foodComboBox.SelectedItem == null)
                return;

            string foodName = foodComboBox.SelectedItem.ToString();
            if (!foodItems.TryGetValue(foodName, out FoodItem foodItem) || foodItem == null)
                return;

            try
            {
                isAnimating = true;
                feedButton.Enabled = false;

                // Воспроизводим звук в зависимости от типа еды
                PlaySound(foodItem.Drink);

                // Загружаем изображение еды
                string foodImagePath = Path.Combine(Application.StartupPath, "Foods", foodName, "image.png");
                Image foodImage = File.Exists(foodImagePath) ?
                    Image.FromFile(foodImagePath) :
                    CreatePlaceholderImage(foodName, 64, 64);

                // Показываем изображение еды
                nikoPicture.Image = foodImage;
                await Task.Delay(1000);

                // Выбираем финальное изображение
                Image finalImage = GetFinalImage(foodItem.Points);
                nikoPicture.Image = finalImage;
                await Task.Delay(1000);

                // Обновляем очки игрока
                playerData.TotalPoints += foodItem.Points;
                SavePlayerData();
                UpdatePointsDisplay();
            }
            catch
            {
                MessageBox.Show("Ошибка при кормлении");
            }
            finally
            {
                nikoPicture.Image = originalImage;
                feedButton.Enabled = true;
                isAnimating = false;
            }
        }

        private void PlaySound(bool isDrink)
        {
            try
            {
                if (isDrink && drinkSound != null)
                {
                    drinkSound.Play();
                }
                else if (!isDrink && eatingSound != null)
                {
                    eatingSound.Play();
                }
            }
            catch
            {
                // Игнорируем ошибки воспроизведения звука
            }
        }

        private Image GetFinalImage(int points)
        {
            if (points >= 7 && points <= 10)
            {
                return eatImage;
            }
            else if (points >= 4 && points <= 6)
            {
                return eatBadImage;
            }
            else
            {
                return eatVeryBadImage;
            }
        }

        private void LoadPlayerData()
        {
            try
            {
                if (!Directory.Exists(userDataPath))
                    Directory.CreateDirectory(userDataPath);

                string filePath = Path.Combine(userDataPath, userDataFile);
                if (File.Exists(filePath))
                {
                    string encryptedData = File.ReadAllText(filePath);
                    string jsonData = Decrypt(encryptedData);
                    playerData = JsonConvert.DeserializeObject<PlayerData>(jsonData) ?? new PlayerData();
                }
            }
            catch
            {
                playerData = new PlayerData();
            }
        }

        private void SavePlayerData()
        {
            try
            {
                if (!Directory.Exists(userDataPath))
                    Directory.CreateDirectory(userDataPath);

                string jsonData = JsonConvert.SerializeObject(playerData);
                string encryptedData = Encrypt(jsonData);
                File.WriteAllText(Path.Combine(userDataPath, userDataFile), encryptedData);
            }
            catch { }
        }

        private void UpdatePointsDisplay()
        {
            pointsLabel.Text = playerData.TotalPoints.ToString();
        }

        private string Encrypt(string plainText)
        {
            return Convert.ToBase64String(Encoding.UTF8.GetBytes(plainText));
        }

        private string Decrypt(string cipherText)
        {
            try
            {
                return Encoding.UTF8.GetString(Convert.FromBase64String(cipherText));
            }
            catch
            {
                return "{\"TotalPoints\":0}";
            }
        }

        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());
        }
    }
}