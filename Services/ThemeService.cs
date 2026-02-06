using SolusManifestApp.Models;
using System;
using System.Linq;
using System.Windows;

namespace SolusManifestApp.Services
{
    public class ThemeService
    {
        public void ApplyTheme(AppTheme theme)
        {
            var themeFile = GetThemeFileName(theme);
            var themeUri = new Uri($"pack://application:,,,/Resources/Themes/{themeFile}", UriKind.Absolute);

            Application.Current.Dispatcher.Invoke(() =>
            {
                // Store other dictionaries (like SteamTheme.xaml)
                var otherDictionaries = Application.Current.Resources.MergedDictionaries
                    .Skip(1)
                    .ToList();

                // Clear all and reload with new theme first
                Application.Current.Resources.MergedDictionaries.Clear();

                // Add new theme first
                var newTheme = new ResourceDictionary { Source = themeUri };
                Application.Current.Resources.MergedDictionaries.Add(newTheme);

                // Re-add other dictionaries
                foreach (var dict in otherDictionaries)
                {
                    Application.Current.Resources.MergedDictionaries.Add(dict);
                }
            });
        }

        private string GetThemeFileName(AppTheme theme)
        {
            return theme switch
            {
                AppTheme.Default => "DefaultTheme.xaml",
                AppTheme.Dark => "DarkTheme.xaml",
                AppTheme.Light => "LightTheme.xaml",
                AppTheme.Cherry => "CherryTheme.xaml",
                AppTheme.Sunset => "SunsetTheme.xaml",
                AppTheme.Forest => "ForestTheme.xaml",
                AppTheme.Grape => "GrapeTheme.xaml",
                AppTheme.Cyberpunk => "CyberpunkTheme.xaml",
                AppTheme.Ocean => "OceanTheme.xaml",
                AppTheme.Midnight => "MidnightTheme.xaml",
                AppTheme.RoseGold => "RoseGoldTheme.xaml",
                AppTheme.Matrix => "MatrixTheme.xaml",
                AppTheme.Ocean2 => "Ocean2Theme.xaml",
                AppTheme.Solar => "SolarTheme.xaml",
                AppTheme.Violet => "VioletTheme.xaml",
                AppTheme.Aurora => "AuroraTheme.xaml",
                AppTheme.DesertSand => "DesertSandTheme.xaml",
                AppTheme.Glacier => "GlacierTheme.xaml",
                AppTheme.Dracula => "DraculaTheme.xaml",
                AppTheme.Nord => "NordTheme.xaml",
                AppTheme.Espresso => "EspressoTheme.xaml",
                AppTheme.Paper => "PaperTheme.xaml",
                AppTheme.Toxicslime => "ToxicslimeTheme.xaml",
                AppTheme.Synthwave => "SynthwaveTheme.xaml",
                AppTheme.Volcanicash => "VolcanicashTheme.xaml",
                AppTheme.Zengarden => "ZengardenTheme.xaml",
                AppTheme.Vaporwave => "VaporwaveTheme.xaml",
                AppTheme.Amethyst => "AmethystTheme.xaml",
                AppTheme.Bubblegumpop => "BubblegumpopTheme.xaml",
                AppTheme.Coffeehouse => "CoffeehouseTheme.xaml",
                AppTheme.Deepspace => "DeepspaceTheme.xaml",
                AppTheme.Emeraldisle => "EmeraldisleTheme.xaml",
                AppTheme.Goldenhour => "GoldenhourTheme.xaml",
                AppTheme.Rubyred => "RubyredTheme.xaml",
                AppTheme.Rustic => "RusticTheme.xaml",
                AppTheme.Sapphireblue => "SapphireblueTheme.xaml",
                _ => "DefaultTheme.xaml"
            };
        }
    }
}
