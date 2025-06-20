
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

        public static void SaveData(Layout layout)
        {
            string filePath = Path.Combine(GetStoragePath(), layout.Id + ".json");
            File.WriteAllText(filePath, layout.ToJson());
        }

        public static Layout? LoadData(int id, ref List<Screen> screens)
        {
            string filePath = Path.Combine(GetStoragePath(), id + ".json");
            if (!File.Exists(filePath))
            {
                return null;
            }
            string json = File.ReadAllText(filePath);
            Layout layout = json.FromJson();
            if (layout == null)
            {
                throw new InvalidOperationException($"Failed to load layout from {filePath}. The file may be corrupted or in an unsupported format.");
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
            var value = System.Text.Json.JsonSerializer.Serialize(layout, SourceGenerationContext.Default.Layout);
            return value;
        }

        public static Layout FromJson(this string json)
        {
            return System.Text.Json.JsonSerializer.Deserialize(json, SourceGenerationContext.Default.Layout) ?? throw new InvalidOperationException("Failed to deserialize Layout from JSON.");
        }
    }
}