
namespace ClemWin
{
    class Storage
    {
        public static string GetStoragePath()
        {
            string appData = Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData);
            string storagePath = Path.Combine(appData, "ClemWin");
            if (!Directory.Exists(storagePath))
            {
                Directory.CreateDirectory(storagePath);
            }
            return storagePath;
        }

        public static void SaveData(string fileName, Layout layout)
        {
            string filePath = Path.Combine(GetStoragePath(), fileName);
            File.WriteAllText(filePath, layout.ToJson());
        }

        public static Layout? LoadData(string fileName, ref List<Screen> screens)
        {
            string filePath = Path.Combine(GetStoragePath(), fileName);
            if (!File.Exists(filePath))
            {
                return null;
            }
            string json = File.ReadAllText(filePath);
            Layout layout = json.FromJson();
            if (layout == null)
            {
                throw new InvalidOperationException($"Failed to load layout from {fileName}. The file may be corrupted or in an unsupported format.");
            }
            foreach (var tile in layout.Tiles)
            {
                bool found = false;
                foreach (var screen in screens)
                {
                    if (tile.Bounds.Screen.Name == screen.Name)
                    {
                        tile.Bounds.Screen = screen;
                        found = true;
                        break;
                    }
                }
                if (!found)
                {
                    screens.Add(tile.Bounds.Screen);
                }
            }
            return layout;
        }
    }

    static class StorageExtensions
    {
        public static string ToJson(this Layout layout)
        {
            return System.Text.Json.JsonSerializer.Serialize(layout, new System.Text.Json.JsonSerializerOptions
            {
                WriteIndented = true
            });
        }

        public static Layout FromJson(this string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize<Layout>(json, new System.Text.Json.JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? throw new InvalidOperationException("Failed to deserialize Layout from JSON.");
        }
    }
}