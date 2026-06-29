using CsvHelper;
using CsvHelper.Configuration;
using TournamentDuprRatings.Models;

namespace TournamentDuprRatings.Services
{
    public class CsvService
    {
        private readonly string _csvFilePathLocation;
        public CsvService(string filePath)
        {
            _csvFilePathLocation = filePath;
        }

        public List<CsvPlayerList> LoadPlayersFromCsv()
        {
            var config = new CsvConfiguration(System.Globalization.CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                Delimiter = ","
            };

            if (File.Exists(_csvFilePathLocation))
            {
                var path = Path.GetFullPath(_csvFilePathLocation);
                using (var reader = new StreamReader(path))
                using (var csv = new CsvReader(reader, config))
                {
                    var records = csv.GetRecords<CsvPlayerList>().ToList();
                    return records;
                }
            }

            throw new Exception($"Failed to find CSV via {_csvFilePathLocation}. Please check the location provided and try again.");
        }
    }
}
