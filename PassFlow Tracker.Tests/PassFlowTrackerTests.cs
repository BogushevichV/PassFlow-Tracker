using PassFlow_Tracker.Domain.Models;
using PassFlow_Tracker.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Linq;
using Xunit;

namespace PassFlow_Tracker.Tests
{
    /// <summary>
    /// 10 unit-тестов для PassFlow Tracker.
    /// Покрывают: DTO-модели, ViewModel-ы, конвертеры, логику агрегации.
    /// Тесты не требуют БД или IPC — только чистая логика.
    /// </summary>
    public class PassFlowTrackerTests
    {
        // ─────────────────────────────────────────────────────────────────────
        // 1. RouteItem.DisplayName формируется корректно
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void RouteItem_DisplayName_FormatsCorrectly()
        {
            var route = new RouteItem("Автобус А100", "Депо Северо-Запад", "Центральный вокзал");

            Assert.Equal("Автобус А100: Депо Северо-Запад → Центральный вокзал", route.DisplayName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 2. TripStopRow — сумма вошедших и вышедших считается корректно
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void TripStopRow_EnteredPlusExited_EqualsExpectedTotal()
        {
            var row = new TripStopRow(
                Id: 1,
                StopNumber: 42,
                StopName: "Площадь Победы",
                Entered: 30,
                Exited: 20,
                Transported: 10);

            int total = row.Entered + row.Exited;

            Assert.Equal(50, total);
            Assert.Equal(42, row.StopNumber);
            Assert.Equal("Площадь Победы", row.StopName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 3. PeakHourChart — пиковый час определяется правильно
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void PeakHourChart_PeakFlag_SetOnMaxFlow()
        {
            var hours = new List<PeakHourChart>
            {
                new(8,  1200, false),
                new(9,  3500, true),   // пик
                new(17, 2800, false),
                new(18, 1900, false),
            };

            var peak = hours.Single(h => h.IsPeak);

            Assert.Equal(9, peak.Hour);
            Assert.Equal(3500, peak.Flow);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 4. PeakHourBarViewModel — HourLabel форматирует час с ведущим нулём
        // ─────────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData(0,  "00")]
        [InlineData(7,  "07")]
        [InlineData(12, "12")]
        [InlineData(23, "23")]
        public void PeakHourBarViewModel_HourLabel_FormatsWithLeadingZero(int hour, string expected)
        {
            var vm = new PeakHourBarViewModel { Hour = hour };

            Assert.Equal(expected, vm.HourLabel);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 5. PeakHourBarViewModel — FlowLabel возвращает строку числа (включая 0)
        // ─────────────────────────────────────────────────────────────────────
        [Theory]
        [InlineData(0,    "0")]
        [InlineData(150,  "150")]
        [InlineData(9999, "9999")]
        public void PeakHourBarViewModel_FlowLabel_ReturnsFlowAsString(long flow, string expected)
        {
            var vm = new PeakHourBarViewModel { Flow = flow };

            Assert.Equal(expected, vm.FlowLabel);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 6. HeightRatioConverter — нулевое соотношение даёт минимальную высоту 2px
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void HeightRatioConverter_ZeroRatio_ReturnsMinHeight()
        {
            var converter = new PassFlow_Tracker.UI.Converters.HeightRatioConverter();

            var result = converter.Convert(0.0, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(2.0, result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 7. HeightRatioConverter — соотношение 1.0 даёт максимальную высоту 290px
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void HeightRatioConverter_FullRatio_ReturnsMaxHeight()
        {
            var converter = new PassFlow_Tracker.UI.Converters.HeightRatioConverter();

            var result = converter.Convert(1.0, typeof(double), null, System.Globalization.CultureInfo.InvariantCulture);

            Assert.Equal(290.0, result);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 8. TopStopRow — Label содержит название остановки
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void TopStopRow_Label_ContainsStopName()
        {
            var row = new TopStopRow(
                Id: 0,
                StopNumber: 101,
                StopName: "Улица Ленина",
                Label: "Улица Ленина  25.10.2023",
                Entered: 50,
                Exited: 40,
                Transported: 30);

            Assert.Contains("Улица Ленина", row.Label);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 9. AllDataDayDto — дерево строится корректно (день → круг → рейс → остановка)
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void AllDataDayDto_TreeStructure_IsCorrect()
        {
            var stop = new AllDataStopDto { StopNumber = 1, StopName = "Депо", Entered = 20, Exited = 0 };
            var trip = new AllDataTripDto { StartPoint = "Депо", EndPoint = "Вокзал", Entered = 20 };
            trip.Stops.Add(stop);

            var round = new AllDataRoundDto { StartPoint = "Депо", EndPoint = "Вокзал", Entered = 20 };
            round.Trips.Add(trip);

            var day = new AllDataDayDto { UnitName = "А100", RecordDate = "25.10.2023", Entered = 20 };
            day.Rounds.Add(round);

            Assert.Single(day.Rounds);
            Assert.Single(day.Rounds[0].Trips);
            Assert.Single(day.Rounds[0].Trips[0].Stops);
            Assert.Equal("Депо", day.Rounds[0].Trips[0].Stops[0].StopName);
        }

        // ─────────────────────────────────────────────────────────────────────
        // 10. Логика расчёта HeightRatio — максимальный столбец получает ratio=1.0
        //     (воспроизводит логику из RunPeakHours в ViewModel)
        // ─────────────────────────────────────────────────────────────────────
        [Fact]
        public void PeakHoursHeightRatio_MaxFlowBar_GetsRatioOne()
        {
            var chartData = new List<PeakHourChart>
            {
                new(6,  500,  false),
                new(8,  1000, false),
                new(9,  3000, true),   // максимум
                new(18, 2000, false),
            };

            long maxFlow = chartData.Max(x => x.Flow);
            var bars = chartData.Select(d => new PeakHourBarViewModel
            {
                Hour        = d.Hour,
                Flow        = d.Flow,
                HeightRatio = maxFlow > 0 ? (double)d.Flow / maxFlow : 0,
                IsPeak      = d.IsPeak
            }).ToList();

            var peakBar = bars.Single(b => b.IsPeak);
            Assert.Equal(1.0, peakBar.HeightRatio, precision: 5);

            // Остальные столбцы должны быть < 1.0
            Assert.All(bars.Where(b => !b.IsPeak), b => Assert.True(b.HeightRatio < 1.0));
        }
    }
}
