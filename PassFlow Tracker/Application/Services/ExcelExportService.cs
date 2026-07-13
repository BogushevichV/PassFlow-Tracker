using ClosedXML.Excel;
using DocumentFormat.OpenXml.Spreadsheet;
using PassFlow_Tracker.UI.ViewModels;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PassFlow_Tracker.Application.Services
{
    public static class ExcelExportService
    {
        public static void ExportTripStopsToExcel(XLWorkbook workbook, ObservableCollection<TripStopRowViewModel> TripStops)
        {
            var ws = workbook.Worksheets.Add("Остановки");

            ws.Cell(1, 1).Value = "№ остановки";
            ws.Cell(1, 2).Value = "Название остановки";
            ws.Cell(1, 3).Value = "Вошло";
            ws.Cell(1, 4).Value = "Вышло";
            ws.Cell(1, 5).Value = "Перевезено";

            var headerRange = ws.Range(1, 1, 1, 7);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x2563EB);
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var stop in TripStops)
            {
                ws.Cell(row, 1).Value = stop.StopNumber;
                ws.Cell(row, 2).Value = stop.StopName;
                ws.Cell(row, 3).Value = stop.Entered;
                ws.Cell(row, 4).Value = stop.Exited;
                ws.Cell(row, 5).Value = stop.Transported;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        public static void ExportTripsToExcel(XLWorkbook workbook, ObservableCollection<TripRowViewModel> Trips)
        {
            var ws = workbook.Worksheets.Add("Рейсы");

            ws.Cell(1, 1).Value = "Автобус";
            ws.Cell(1, 2).Value = "Откуда";
            ws.Cell(1, 3).Value = "Куда";
            ws.Cell(1, 4).Value = "Время От";
            ws.Cell(1, 5).Value = "Время До";
            ws.Cell(1, 6).Value = "Вошло";
            ws.Cell(1, 7).Value = "Вышло";
            ws.Cell(1, 8).Value = "Перевезено";

            var headerRange = ws.Range(1, 1, 1, 8);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0xB45309);
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var trip in Trips)
            {
                ws.Cell(row, 1).Value = trip.UnitName;
                ws.Cell(row, 2).Value = trip.StartPoint;
                ws.Cell(row, 3).Value = trip.EndPoint;
                ws.Cell(row, 4).Value = trip.TimeFrom;
                ws.Cell(row, 5).Value = trip.TimeTo;
                ws.Cell(row, 6).Value = trip.Entered;
                ws.Cell(row, 7).Value = trip.Exited;
                ws.Cell(row, 8).Value = trip.Transported;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        public static void ExportRoundsToExcel(XLWorkbook workbook, ObservableCollection<RoundRowViewModel> Rounds)
        {
            var ws = workbook.Worksheets.Add("Круги");

            ws.Cell(1, 1).Value = "Автобус";
            ws.Cell(1, 2).Value = "Откуда";
            ws.Cell(1, 3).Value = "Куда";
            ws.Cell(1, 4).Value = "Время От";
            ws.Cell(1, 5).Value = "Время До";
            ws.Cell(1, 6).Value = "Вошло";
            ws.Cell(1, 7).Value = "Вышло";
            ws.Cell(1, 8).Value = "Перевезено";

            var headerRange = ws.Range(1, 1, 1, 8);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x16A34A);
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var round in Rounds)
            {
                ws.Cell(row, 1).Value = round.UnitName;
                ws.Cell(row, 2).Value = round.StartPoint;
                ws.Cell(row, 3).Value = round.EndPoint;
                ws.Cell(row, 4).Value = round.TimeFrom;
                ws.Cell(row, 5).Value = round.TimeTo;
                ws.Cell(row, 6).Value = round.Entered;
                ws.Cell(row, 7).Value = round.Exited;
                ws.Cell(row, 8).Value = round.Transported;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        public static void ExportDailyRecordsToExcel(XLWorkbook workbook, ObservableCollection<DailyRecordRowViewModel> DailyRecords)
        {
            var ws = workbook.Worksheets.Add("Дни");

            ws.Cell(1, 1).Value = "Автобус";
            ws.Cell(1, 2).Value = "Дата";
            ws.Cell(1, 3).Value = "Вошло";
            ws.Cell(1, 4).Value = "Вышло";
            ws.Cell(1, 5).Value = "Перевезено";

            var headerRange = ws.Range(1, 1, 1, 5);
            headerRange.Style.Font.Bold = true;
            headerRange.Style.Fill.BackgroundColor = XLColor.FromArgb(0x1D4ED8);
            headerRange.Style.Font.FontColor = XLColor.White;

            int row = 2;
            foreach (var day in DailyRecords)
            {
                ws.Cell(row, 1).Value = day.UnitName;
                ws.Cell(row, 2).Value = day.RecordDate;
                ws.Cell(row, 3).Value = day.Entered;
                ws.Cell(row, 4).Value = day.Exited;
                ws.Cell(row, 5).Value = day.Transported;
                row++;
            }

            ws.Columns().AdjustToContents();
        }

        public static void ExportAllDataToExcel(XLWorkbook workbook, ObservableCollection<DayNodeViewModel> AllDataTree)
        {
            foreach (var day in AllDataTree)
            {
                var sheetName = $"{day.UnitName} {day.RecordDate}";
                var ws = AddUniqueWorksheet(workbook, sheetName);

                int row = 1;

                ws.Cell(row, 1).Value = "Автобус";
                ws.Cell(row, 2).Value = day.UnitName;
                ws.Range(row, 1, row, 2).Style.Font.Bold = true;
                ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(0xEFF6FF);
                row++;

                ws.Cell(row, 1).Value = "Дата";
                ws.Cell(row, 2).Value = day.RecordDate;
                ws.Range(row, 1, row, 2).Style.Fill.BackgroundColor = XLColor.FromArgb(0xEFF6FF);
                row++;

                ws.Cell(row, 1).Value = "Вошло";
                ws.Cell(row, 2).Value = day.Entered;
                ws.Cell(row, 2).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(row, 3).Value = "Вышло";
                ws.Cell(row, 4).Value = day.Exited;
                ws.Cell(row, 4).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Cell(row, 5).Value = "Перевезено";
                ws.Cell(row, 6).Value = day.Transported;
                ws.Cell(row, 6).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                ws.Range(row, 1, row, 6).Style.Fill.BackgroundColor = XLColor.FromArgb(0xEFF6FF);
                row += 2;

                if (day.Rounds.Any())
                {
                    ws.Cell(row, 1).Value = "КРУГИ";
                    ws.Range(row, 1, row, 7).Merge();
                    ws.Range(row, 1, row, 7).Style.Font.Bold = true;
                    ws.Range(row, 1, row, 7).Style.Fill.BackgroundColor = XLColor.FromArgb(0xF0FDF4);
                    row++;

                    ws.Cell(row, 1).Value = "Откуда";
                    ws.Cell(row, 2).Value = "Куда";
                    ws.Cell(row, 3).Value = "Время От";
                    ws.Cell(row, 4).Value = "Время До";
                    ws.Cell(row, 5).Value = "Вошло";
                    ws.Cell(row, 6).Value = "Вышло";
                    ws.Cell(row, 7).Value = "Перевезено";
                    ws.Range(row, 1, row, 7).Style.Font.Bold = true;
                    row++;

                    foreach (var round in day.Rounds)
                    {
                        ws.Cell(row, 1).Value = round.StartPoint;
                        ws.Cell(row, 2).Value = round.EndPoint;
                        ws.Cell(row, 3).Value = round.TimeFrom;
                        ws.Cell(row, 4).Value = round.TimeTo;
                        ws.Cell(row, 5).Value = round.Entered;
                        ws.Cell(row, 6).Value = round.Exited;
                        ws.Cell(row, 7).Value = round.Transported;
                        row++;

                        if (round.Trips.Any())
                        {
                            ws.Cell(row, 2).Value = "РЕЙСЫ";
                            ws.Range(row, 2, row, 8).Style.Font.Bold = true;
                            ws.Range(row, 2, row, 8).Style.Fill.BackgroundColor = XLColor.FromArgb(0xFFFBEB);
                            row++;

                            ws.Cell(row, 2).Value = "Откуда";
                            ws.Cell(row, 3).Value = "Куда";
                            ws.Cell(row, 4).Value = "Время От";
                            ws.Cell(row, 5).Value = "Время До";
                            ws.Cell(row, 6).Value = "Вошло";
                            ws.Cell(row, 7).Value = "Вышло";
                            ws.Cell(row, 8).Value = "Перевезено";
                            row++;

                            foreach (var trip in round.Trips)
                            {
                                ws.Cell(row, 2).Value = trip.StartPoint;
                                ws.Cell(row, 3).Value = trip.EndPoint;
                                ws.Cell(row, 4).Value = trip.TimeFrom;
                                ws.Cell(row, 5).Value = trip.TimeTo;
                                ws.Cell(row, 6).Value = trip.Entered;
                                ws.Cell(row, 7).Value = trip.Exited;
                                ws.Cell(row, 8).Value = trip.Transported;
                                row++;

                                if (trip.Stops.Any())
                                {
                                    ws.Cell(row, 3).Value = "ОСТАНОВКИ";
                                    ws.Range(row, 3, row, 11).Style.Font.Bold = true;
                                    ws.Range(row, 3, row, 11).Style.Fill.BackgroundColor = XLColor.FromArgb(0xfdefff);
                                    row++;

                                    ws.Cell(row, 3).Value = "№";
                                    ws.Cell(row, 4).Value = "Название";
                                    ws.Cell(row, 5).Value = "Время От";
                                    ws.Cell(row, 6).Value = "Время До";
                                    ws.Cell(row, 7).Value = "Вошло";
                                    ws.Cell(row, 8).Value = "Вышло";
                                    ws.Cell(row, 9).Value = "Перевезено";
                                    ws.Cell(row, 10).Value = "Дубль";
                                    ws.Cell(row, 11).Value = "Пропуск";
                                    row++;

                                    foreach (var stop in trip.Stops)
                                    {
                                        ws.Cell(row, 3).Value = stop.StopNumber;
                                        ws.Cell(row, 3).Style.Alignment.Horizontal = XLAlignmentHorizontalValues.Left;
                                        ws.Cell(row, 4).Value = stop.StopName;
                                        ws.Cell(row, 5).Value = stop.TimeFrom;
                                        ws.Cell(row, 6).Value = stop.TimeTo;
                                        ws.Cell(row, 7).Value = stop.Entered;
                                        ws.Cell(row, 8).Value = stop.Exited;
                                        ws.Cell(row, 9).Value = stop.Transported;
                                        ws.Cell(row, 10).Value = stop.IsDuplicate ? "Да" : "";
                                        ws.Cell(row, 11).Value = stop.IsSkipped ? "Да" : "";
                                        row++;
                                    }
                                }
                            }
                        }
                        row++; 
                    }
                }

                ws.Columns().AdjustToContents();
            }
        }

        public static IXLWorksheet AddUniqueWorksheet(XLWorkbook workbook, string baseName)
        {
            var cleanName = baseName
                .Replace(":", "")
                .Replace("\\", "-")
                .Replace("/", "-")
                .Replace("?", "")
                .Replace("*", "")
                .Replace("[", "")
                .Replace("]", "");

            if (cleanName.Length > 31)
                cleanName = cleanName[..31];

            if (!workbook.Worksheets.Any(w => w.Name == cleanName))
            {
                return workbook.Worksheets.Add(cleanName);
            }

            int counter = 1;
            string candidateName;

            do
            {
                counter++;
                var suffix = $" ({counter})";

                var maxBaseLength = 31 - suffix.Length;

                if (cleanName.Length > maxBaseLength)
                    candidateName = cleanName[..maxBaseLength] + suffix;
                else
                    candidateName = cleanName + suffix;

            } while (workbook.Worksheets.Any(w => w.Name == candidateName));

            return workbook.Worksheets.Add(candidateName);
        }
    }
}
