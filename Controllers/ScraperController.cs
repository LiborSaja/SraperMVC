using HtmlAgilityPack;
using Microsoft.AspNetCore.Mvc;
using Scraper.Models;
using System;
using System.Text.Json;
using System.Xml.Serialization;

namespace Scraper.Controllers {
    public class ScraperController : Controller {

        #region Returning views of corresponding pages
        // vrácení view pro zobrazení odpovídajících stránek
        [HttpGet]
        public IActionResult Index() {
            return View();
        }

        [HttpGet]
        public IActionResult About() {
            return View();
        }
        #endregion

        #region Logic of Index page
        // získání vstupu od uživatele a uložení do proměnné 'keyword'
        [HttpPost]
        public async Task<IActionResult> Index(string keyword) {
            // podmínka pro prázdný vstup
            if (string.IsNullOrWhiteSpace(keyword)) {
                ViewBag.Error = "Klíčové slovo je povinné!";
                return View();
            }
            // scraping -> extrakce dat z webu pomocí HttpClient a HtmlAgilityPack
            var results = await GetGoogleResultsAsync(keyword);
            // podmínka pokud GetGoogleResultsAsync() vrátí null
            if (results == null || !results.Any()) {
                ViewBag.Error = "Nebyly nalezeny žádné výsledky pro zadané klíčové slovo.";
                return View();
            }

            // uložení výsledků do JSON, XML, CSV
            await SaveResultsAsJsonAsync(results);
            await SaveResultsAsXmlAsync(results);
            await SaveResultsAsCsvAsync(results);

            return View(results);
        }
        #endregion

        #region Result method for logic of Index page
        // metoda pro sestavení url požadavku, stažení html obsahu, analýzu html, extrahování dat a uložení výsledků do kolekce - pole objektů
        private async Task<List<SearchResult>> GetGoogleResultsAsync(string keyword) {            
            var url = $"https://www.google.com/search?q={Uri.EscapeDataString(keyword)}";            
            var httpClient = new HttpClient();
            httpClient.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/91.0.4472.124 Safari/537.36");

            // stažení html stránky a zpracování pomocí Xpath (Dotazovací jazyk pro výběr uzlů v XML nebo HTML)
            var response = await httpClient.GetStringAsync(url);      
            var htmlDoc = new HtmlDocument();
            htmlDoc.LoadHtml(response);

            // uložení všech extrahovanýh informací z výsledků vyhledávání
            var results = new List<SearchResult>();

            // xpath pro všechny výsledky na stránce -> výsledek je seznam uzlů, kde každý uzel reprezentuje jeden výsledek vyhledávání.
            var nodes = htmlDoc.DocumentNode.SelectNodes("//div[contains(@class, 'tF2Cxc')]");
            if (nodes != null) {
                foreach (var node in nodes) {
                    var titleNode = node.SelectSingleNode(".//h3");
                    var linkNode = node.SelectSingleNode(".//a");
                    var snippetNode = node.SelectSingleNode(".//div[contains(@class, 'VwiC3b')]");
                    var iconNode = node.SelectSingleNode(".//img[contains(@class, 'XNo5Ab')]"); 

                    results.Add(new SearchResult {
                        Title = titleNode?.InnerText?.Trim(),
                        Link = linkNode?.GetAttributeValue("href", string.Empty)?.Trim(),
                        Snippet = snippetNode?.InnerText?.Trim(),
                        Icon = iconNode?.GetAttributeValue("src", string.Empty)?.Trim() 
                    });
                }
            }

            return results;
        }
        #endregion

        #region Serialization and saving results to JSON
        // serializace z objektu na JSON formát, povolení českých znaků v JSON souboru
        private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions {
            WriteIndented = true,
            Encoder = System.Text.Encodings.Web.JavaScriptEncoder.Create(
                System.Text.Unicode.UnicodeRanges.BasicLatin,
                System.Text.Unicode.UnicodeRanges.Latin1Supplement,
                System.Text.Unicode.UnicodeRanges.LatinExtendedA
            )
        };

        // získání a uložení výsledků do JSON
        private async Task SaveResultsAsJsonAsync(List<SearchResult> results) {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Results.json");
            var jsonData = JsonSerializer.Serialize(results, JsonOptions);
            await System.IO.File.WriteAllTextAsync(filePath, jsonData);
        }

        // uložení do zařízení ve formátu JSON
        [HttpGet]
        public IActionResult DownloadJson() {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Results.json");
            if (!System.IO.File.Exists(filePath)) {
                return NotFound("Soubor neexistuje.");
            }
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/json", "Results.json");
        }
        #endregion

        #region Getting and saving results to XML
        // získání a uložení výsledků do XML
        private async Task SaveResultsAsXmlAsync(List<SearchResult> results) {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Results.xml");

            // Serializace do XML
            var serializer = new XmlSerializer(typeof(List<SearchResult>));
            await using var stream = new FileStream(filePath, FileMode.Create);
            serializer.Serialize(stream, results);
        }

        [HttpGet]
        public IActionResult DownloadXml() {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Results.xml");
            if (!System.IO.File.Exists(filePath)) {
                return NotFound("Soubor XML neexistuje.");
            }
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "application/xml", "Results.xml");
        }
        #endregion

        #region Getting and saving results to CSV
        // získání a uložení výsledků do CSV
        private async Task SaveResultsAsCsvAsync(List<SearchResult> results) {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Results.csv");
            // vytvoření CSV řádků
            var csvLines = new List<string> { "Title,Link,Snippet,Icon" }; // hlavička
            csvLines.AddRange(results.Select(r =>
                $"\"{r.Title}\",\"{r.Link}\",\"{r.Snippet}\",\"{r.Icon}\""
            ));
            // uložení CSV do souboru
            await System.IO.File.WriteAllLinesAsync(filePath, csvLines);
        }

        [HttpGet]
        public IActionResult DownloadCsv() {
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "Results.csv");
            if (!System.IO.File.Exists(filePath)) {
                return NotFound("Soubor CSV neexistuje.");
            }
            var fileBytes = System.IO.File.ReadAllBytes(filePath);
            return File(fileBytes, "text/csv", "Results.csv");
        }
        #endregion

    }
}
