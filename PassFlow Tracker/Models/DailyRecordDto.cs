using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Models
{
    // Транспортное средство
    public class RootRecord
    {
        [JsonPropertyName("unit")]
        public string Unit { get; set; }

        [JsonPropertyName("date")]
        public string Date { get; set; }

        [JsonPropertyName("count")]
        public CountDto Count { get; set; }

        [JsonPropertyName("rounds")]
        public List<RoundDto> Rounds { get; set; }
    }

    // Показатели количества пассажиров
    public class CountDto
    {
        [JsonPropertyName("entered")]
        public int Entered { get; set; }

        [JsonPropertyName("exited")]
        public int Exited { get; set; }

        [JsonPropertyName("transported")]
        public int Transported { get; set; }
    }

    // Круг за дату
    public class RoundDto
    {
        [JsonPropertyName("start")]
        public string Start { get; set; }

        [JsonPropertyName("end")]
        public string End { get; set; }

        [JsonPropertyName("timeFrom")]
        public string TimeFrom { get; set; }

        [JsonPropertyName("timeTo")]
        public string TimeTo { get; set; }

        [JsonPropertyName("count")]
        public CountDto Count { get; set; }

        [JsonPropertyName("trips")]
        public List<TripDto> Trips { get; set; }
    }

    // Рейс за круг
    public class TripDto
    {
        [JsonPropertyName("start")]
        public string Start { get; set; }

        [JsonPropertyName("end")]
        public string End { get; set; }

        [JsonPropertyName("timeFrom")]
        public string TimeFrom { get; set; }

        [JsonPropertyName("timeTo")]
        public string TimeTo { get; set; }

        [JsonPropertyName("count")]
        public CountDto Count { get; set; }

        [JsonPropertyName("stops")]
        public List<StopDto> Stops { get; set; }
    }

    // Остановка за рейс 
    public class StopDto
    {
        [JsonPropertyName("id")]
        public int Id { get; set; }

        [JsonPropertyName("name")]
        public string Name { get; set; }

        [JsonPropertyName("duplicate")]
        public bool Duplicate { get; set; }

        [JsonPropertyName("skipped")]
        public bool Skipped { get; set; }

        [JsonPropertyName("timeFrom")]
        public string TimeFrom { get; set; }

        [JsonPropertyName("timeTo")]
        public string TimeTo { get; set; }

        [JsonPropertyName("count")]
        public CountDto Count { get; set; }
    }
}
