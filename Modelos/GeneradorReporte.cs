using System;
using System.Collections.Generic;
using System.Linq;
// --- ALIAS PARA EVITAR CONFLICTOS ---
using QuestPDF.Fluent;
using QuestPDF.Helpers;
using QuestPDF.Infrastructure;
using QColors = QuestPDF.Helpers.Colors; // "QColors" será para el PDF
using SPlot = ScottPlot; // "SPlot" será para los gráficos

namespace SagaIngenieria .Modelos
{
    public static class GeneradorReporte
    {
        public static void Generar(Modelos.Ensayo ensayo, string rutaArchivo)
        {
            // Configuración de licencia (Si tienes una versión vieja, borra esta línea)
            QuestPDF.Settings.License = LicenseType.Community;

            // 1. PREPARAR DATOS
            var puntos = DeserializarPuntos(ensayo.DatosCrudos);
            byte[] imagenGrafico = GenerarImagenParaReporte(puntos);

            // 2. DISEÑAR EL PDF
            Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Size(PageSizes.A4);
                    page.Margin(2, Unit.Centimetre);
                    page.PageColor(QColors.White);
                    page.DefaultTextStyle(x => x.FontSize(11).FontFamily("Arial"));

                    // --- ENCABEZADO ---
                    page.Header().Row(row =>
                    {
                        row.RelativeItem().Column(col =>
                        {
                            col.Item().Text("SAGA INGENIERÍA").FontSize(20).SemiBold().FontColor(QColors.Blue.Medium);
                            col.Item().Text("Informe Técnico de Amortiguador").FontSize(10).FontColor(QColors.Grey.Medium);
                        });

                        row.ConstantItem(100).Column(col =>
                        {
                            col.Item().Text(DateTime.Now.ToString("dd/MM/yyyy")).AlignRight();
                            col.Item().Text($"ID: {ensayo.Id:D6}").AlignRight();
                        });
                    });

                    // --- CONTENIDO ---
                    page.Content().PaddingVertical(1, Unit.Centimetre).Column(col =>
                    {
                        // A. Datos del Cliente
                        col.Item().Border(1).BorderColor(QColors.Grey.Lighten2).Padding(10).Row(row =>
                        {
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("CLIENTE").FontSize(9).FontColor(QColors.Grey.Darken2).Bold();
                                c.Item().Text(ensayo.Vehiculo?.Cliente?.Nombre ?? "N/A").FontSize(12);
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("VEHÍCULO").FontSize(9).FontColor(QColors.Grey.Darken2).Bold();
                                c.Item().Text(ensayo.Vehiculo?.Modelo ?? "N/A").FontSize(12);
                            });
                            row.RelativeItem().Column(c =>
                            {
                                c.Item().Text("FECHA").FontSize(9).FontColor(QColors.Grey.Darken2).Bold();
                                c.Item().Text(ensayo.Fecha.ToString("g")).FontSize(12);
                            });
                        });

                        col.Item().Height(10);

                        // B. Tabla de Resultados
                        col.Item().Table(table =>
                        {
                            table.ColumnsDefinition(columns =>
                            {
                                columns.RelativeColumn();
                                columns.RelativeColumn();
                            });

                            table.Header(header =>
                            {
                                header.Cell().Element(EstiloCelda).Text("Max. Compresión").SemiBold();
                                header.Cell().Element(EstiloCelda).Text("Max. Expansión").SemiBold();
                            });

                            table.Cell().Element(EstiloCeldaDato).Text($"{ensayo.MaxCompresion:F1} kg").FontColor(QColors.Red.Medium);
                            table.Cell().Element(EstiloCeldaDato).Text($"{ensayo.MaxExpansion:F1} kg").FontColor(QColors.Blue.Medium);
                        });

                        col.Item().Height(20);

                        // C. Gráfico
                        col.Item().Text("Curva de Histéresis").FontSize(12).SemiBold();
                        if (imagenGrafico != null)
                        {
                            col.Item().Image(imagenGrafico).FitWidth();
                        }
                        else
                        {
                            col.Item().Text("[Sin datos gráficos]").FontColor(QColors.Red.Medium);
                        }

                        col.Item().Height(10);

                        // D. Notas
                        col.Item().Background(QColors.Grey.Lighten4).Padding(10).Column(c =>
                        {
                            c.Item().Text("Observaciones:").FontSize(10).Bold();
                            c.Item().Text(ensayo.Notas).FontSize(10).Italic();
                        });
                    });

                    // --- PIE DE PÁGINA ---
                    page.Footer().AlignCenter().Text(x =>
                    {
                        x.Span("Generado por SAGA v2.0 - ");
                        x.CurrentPageNumber();
                        x.Span(" / ");
                        x.TotalPages();
                    });
                });
            })
            .GeneratePdf(rutaArchivo);
        }

        // Funciones auxiliares de Estilo
        static IContainer EstiloCelda(IContainer container)
        {
            return container.Background(QColors.Grey.Lighten3).Padding(5).BorderBottom(1).BorderColor(QColors.White);
        }

        static IContainer EstiloCeldaDato(IContainer container)
        {
            return container.Padding(10).AlignCenter();
        }

        private static byte[] GenerarImagenParaReporte(List<PuntoDeEnsayo> puntos)
        {
            if (puntos == null || puntos.Count == 0) return null;

            // Usamos SPlot (ScottPlot) explícitamente
            var plot = new SPlot.Plot();

            // Configuración visual para impresión (Blanco y Negro / Limpio)
            plot.FigureBackground.Color = SPlot.Color.FromHex("#FFFFFF");
            plot.DataBackground.Color = SPlot.Color.FromHex("#FFFFFF");
            plot.Grid.MajorLineColor = SPlot.Color.FromHex("#E0E0E0");
            plot.Axes.Color(SPlot.Color.FromHex("#000000"));

            var xs = puntos.Select(p => p.Posicion).ToArray();
            var ys = puntos.Select(p => p.Fuerza).ToArray();

            var linea = plot.Add.Scatter(xs, ys);
            linea.Color = SPlot.Color.FromHex("#003366"); // Azul Oscuro
            linea.LineWidth = 2;

            plot.Title("Dinámica del Amortiguador");
            plot.Axes.Bottom.Label.Text = "Desplazamiento (mm)";
            plot.Axes.Left.Label.Text = "Fuerza (kg)";
            plot.Axes.AutoScale();

            // Generar imagen PNG en memoria
            return plot.GetImage(800, 600).GetImageBytes();
        }

        private static List<PuntoDeEnsayo> DeserializarPuntos(byte[] datos)
        {
            try
            {
                if (datos == null || datos.Length == 0) return null;
                string json = System.Text.Encoding.UTF8.GetString(datos);
                return System.Text.Json.JsonSerializer.Deserialize<List<PuntoDeEnsayo>>(json);
            }
            catch { return null; }
        }
    }
}
