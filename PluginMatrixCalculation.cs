using System;
using System.IO;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Text;
using Autodesk.AutoCAD.Runtime;
using Autodesk.AutoCAD.DatabaseServices;
using Autodesk.AutoCAD.PlottingServices;
using Autodesk.AutoCAD.ApplicationServices;
using Autodesk.AutoCAD.EditorInput;
using Autodesk.AutoCAD.Geometry;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PluginMatrixCalculation.models;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;



namespace PluginMatrixCalculation
{
    public class Commands : IExtensionApplication
    {
        private long _calculationId;
        
        [CommandMethod("MakeCalculation")]
        public void MakeCalculation()
        {
            Database db = HostApplicationServices.WorkingDatabase;
            short bgp = (short)AcAp.GetSystemVariable("BACKGROUNDPLOT");
            Editor ed = AcAp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                ed.WriteMessage("Начинается расчёт параметров электрической сети...");
                
                HttpClient client = new HttpClient();

                var model = new CalculationCreateModel(
                    _networkTopology, 
                    _branchResistances, 
                    _branchEVMs, 
                    _branchCurrentSources, 
                    _towersNumbers, 
                    _calculationPoints);

                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                string requestString = JsonConvert.SerializeObject(model, jsonSerializerSettings);
                
                var stringContent = new StringContent(requestString, Encoding.UTF8, "application/json");
                
                var response = client.PostAsync("http://localhost:8080/api/calculation", stringContent).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;

                CalculationResultsModel results = 
                    JsonConvert.DeserializeObject<CalculationResultsModel>(responseString, jsonSerializerSettings);

                _calculationId = results.CalculationId;
                
                CreateResultTables(db, ed, results);

                ed.WriteMessage("\nРасчёт параметров электрической сети выполнен");
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\nError: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                AcAp.SetSystemVariable("BACKGROUNDPLOT", bgp);
                File.Delete(Path.Combine(Environment.CurrentDirectory, "plot.log"));
            }
        }
        
        [CommandMethod("CheckCalculation")]
        public void CheckCalculation()
        {
            Database db = HostApplicationServices.WorkingDatabase;
            short bgp = (short)AcAp.GetSystemVariable("BACKGROUNDPLOT");
            Editor ed = AcAp.DocumentManager.MdiActiveDocument.Editor;
            try
            {
                if (_calculationId == 0)
                {
                    ed.WriteMessage("Для проверки параметров электрической сети необходимо произвести расчёт");
                    return;
                }

                ed.WriteMessage("Начинается проверка параметров электрической сети...");
                
                HttpClient client = new HttpClient();
                

                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();

                var response = client.GetAsync($"http://localhost:8080/api/calculation/{_calculationId}/check").Result;
                var responseString = response.Content.ReadAsStringAsync().Result;

                List<CheckResult> checkResults = 
                    JsonConvert.DeserializeObject<List<CheckResult>>(responseString, jsonSerializerSettings);

                CreateCheckResultTable(db, ed, checkResults);
                    
                ed.WriteMessage("\nПроверка параметров электрической сети выполнен");
            }
            catch (System.Exception e)
            {
                ed.WriteMessage($"\nError: {e.Message}\n{e.StackTrace}");
            }
            finally
            {
                AcAp.SetSystemVariable("BACKGROUNDPLOT", bgp);
                File.Delete(Path.Combine(Environment.CurrentDirectory, "plot.log"));
            }
        }

        private void CreateResultTables(Database db, Editor ed, CalculationResultsModel resultsModel)
        {
            CreateResultTableForCTS();
            
            resultsModel.ResultsForTowers.ForEach(x => CreateResultTableForTower(x));

            void CreateResultTableForCTS()
            {
                PromptPointResult pr = ed.GetPoint("\nВыберете точку для вставки таблицы результатов для КТП");

                if (pr.Status == PromptStatus.OK)
                {
                    ObjectId textStyleId = ObjectId.Null;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        String textStyleName = "СПДС";
                        TextStyleTable tst = tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite) as TextStyleTable;

                        if (tst.Has(textStyleName))
                        {
                            textStyleId = tst[textStyleName];
                        }
                        tr.Commit();
                    }
                    
                    Table tb = new Table();
                    tb.TableStyle = db.Tablestyle;
                    tb.SetSize(4, 2);
                    tb.SetRowHeight(4);
                    tb.SetColumnWidth(10);
                    tb.Position = pr.Value;
                    if (textStyleId != ObjectId.Null) tb.Cells.TextStyleId = textStyleId;
                    tb.Cells.Alignment = CellAlignment.MiddleCenter;
                    tb.Cells.TextHeight = 1.8;
                    
                    tb.MergeCells(CellRange.Create(tb,0,0,0,1));

                    var resultsForCTS = resultsModel.ResultFotCTS;
                    tb.Cells[0, 0].TextString = "КТП";
                    tb.Cells[1, 0].TextString = "Iф.A";
                    tb.Cells[1, 1].TextString = GetAmpereValueText(resultsForCTS.CurrentPhaseA);
                    tb.Cells[2, 0].TextString = "Iф.B";
                    tb.Cells[2, 1].TextString = GetAmpereValueText(resultsForCTS.CurrentPhaseB);
                    tb.Cells[3, 0].TextString = "Iф.C";
                    tb.Cells[3, 1].TextString = GetAmpereValueText(resultsForCTS.CurrentPhaseC);

                    tb.GenerateLayout();
                    AppendTable(db, tb);
                }
            }
            
            void CreateResultTableForTower(CalculationResultForTower resultForTower)
            {
                PromptPointResult pr = ed.GetPoint(
                    $"\nВыберете точку для вставки таблицы результатов для опоры {resultForTower.TowerNumber}");

                if (pr.Status == PromptStatus.OK)
                {
                    ObjectId textStyleId = ObjectId.Null;
                    using (Transaction tr = db.TransactionManager.StartTransaction())
                    {
                        String textStyleName = "СПДС";
                        TextStyleTable tst = tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite) as TextStyleTable;

                        if (tst.Has(textStyleName))
                        {
                            textStyleId = tst[textStyleName];
                        }
                        tr.Commit();
                    }
                    
                    Table tb = new Table();
                    tb.TableStyle = db.Tablestyle;
                    tb.SetSize(5, 2);
                    tb.SetRowHeight(4);
                    tb.SetColumnWidth(10);
                    tb.Position = pr.Value;
                    if (textStyleId != ObjectId.Null) tb.Cells.TextStyleId = textStyleId;
                    tb.Cells.Alignment = CellAlignment.MiddleCenter;
                    tb.Cells.TextHeight = 1.8;
                    
                    tb.MergeCells(CellRange.Create(tb,0,0,0,1));

                    tb.Cells[0, 0].TextString = $"Опора {resultForTower.TowerNumber}";
                    tb.Cells[1, 0].TextString = "Uф.A";
                    tb.Cells[1, 1].TextString = GetVoltageValueText(resultForTower.VoltagePhaseA);
                    tb.Cells[2, 0].TextString = "Uф.B";
                    tb.Cells[2, 1].TextString = GetVoltageValueText(resultForTower.VoltagePhaseB);
                    tb.Cells[3, 0].TextString = "Uф.C";
                    tb.Cells[3, 1].TextString = GetVoltageValueText(resultForTower.VoltagePhaseC);
                    tb.Cells[4, 0].TextString = "ΔU% max";
                    tb.Cells[4, 1].TextString = GetPercentValueText(resultForTower.MaxVoltageLoss);

                    tb.GenerateLayout();
                    AppendTable(db, tb);
                }
            }
        }

        private void CreateCheckResultTable(Database db, Editor ed, List<CheckResult> checkResults)
        {
            PromptPointResult pr = ed.GetPoint("\nВыберете точку для вставки таблицы проверки результатов");

            if (pr.Status == PromptStatus.OK)
            {
                ObjectId textStyleId = ObjectId.Null;
                using (Transaction tr = db.TransactionManager.StartTransaction())
                {
                    String textStyleName = "СПДС";
                    TextStyleTable tst = tr.GetObject(db.TextStyleTableId, OpenMode.ForWrite) as TextStyleTable;

                    if (tst.Has(textStyleName))
                    {
                        textStyleId = tst[textStyleName];
                    }
                    tr.Commit();
                }
                    
                Table tb = new Table();
                tb.TableStyle = db.Tablestyle;
                tb.SetSize(checkResults.Count + 2, 3);
                tb.SetRowHeight(4);
                tb.SetColumnWidth(10);
                tb.Position = pr.Value;
                if (textStyleId != ObjectId.Null) tb.Cells.TextStyleId = textStyleId;
                tb.Cells.Alignment = CellAlignment.MiddleCenter;
                tb.Cells.TextHeight = 1.8;
                    
                tb.MergeCells(CellRange.Create(tb,0,0,0,2));
                
                tb.Cells[0, 0].TextString = "Проверка по ΔU%";
                tb.Cells[1, 0].TextString = "№ опоры";
                tb.Cells[1, 1].TextString = "ΔU%";
                tb.Cells[1, 2].TextString = "Вердикт";
                for (int i = 0; i < checkResults.Count; i++)
                {
                    CheckResult checkResult = checkResults[i];
                    tb.Cells[i + 2, 0].TextString = checkResult.TowerNumber;
                    tb.Cells[i + 2, 1].TextString = GetPercentValueText(checkResult.MaxVoltageLoss);
                    tb.Cells[i + 2, 2].TextString = checkResult.IsValid ? "OK" : "NOT OK";
                }

                tb.GenerateLayout();
                AppendTable(db, tb);
            }
        }
        
        void AppendTable(Database db, Table tb)
        {
            Transaction tr = db.TransactionManager.StartTransaction();
            using (tr)
            {
                BlockTable bt =
                    (BlockTable)tr.GetObject(
                        db.BlockTableId,
                        OpenMode.ForRead
                    );
                BlockTableRecord btr =
                    (BlockTableRecord)tr.GetObject(
                        bt[BlockTableRecord.ModelSpace],
                        OpenMode.ForWrite
                    );
                    
                btr.AppendEntity(tb);
                tr.AddNewlyCreatedDBObject(tb, true);
                tr.Commit();
            }
        }

        public void Initialize() { }
        public void Terminate() { }
        string GetAmpereValueText(ComplexNumber complexValue) => $"{complexValue} A";
        string GetVoltageValueText(ComplexNumber complexValue) => $"{complexValue} B";
        string GetPercentValueText(double value) => $"{Math.Round(value, 2).ToString(CultureInfo.CurrentCulture)} %";
        
        private List<List<int>> _networkTopology = new List<List<int>> 
        {
           new List<int> { -1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0,-1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0,-1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 1,-1,-1,-1, 0, 0, 0,-1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,-1,-1,-1, 0, 0, 0,-1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,-1,-1,-1, 0, 0, 0,-1, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0, 1, 0, 0, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 1,-1,-1,-1 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1, 0 },
           new List<int> {  0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0,-1, 0, 0, 0, 1 }
        };

        private List<ComplexNumber> _branchResistances = new List<ComplexNumber>
        {
            new ComplexNumber(0.00947, 0.0272),
            new ComplexNumber(0.00947, 0.0272),
            new ComplexNumber(0.00947, 0.0272),

            new ComplexNumber(0.003408, 0.000471),
            new ComplexNumber(0.003408, 0.000471),
            new ComplexNumber(0.003408, 0.000471),
            new ComplexNumber(0.005538, 0.0004074),

            new ComplexNumber(39.71879,11.58465),
            new ComplexNumber(39.71879,11.58465),
            new ComplexNumber(39.71879,11.58465),

            new ComplexNumber(0.01988, 0.0027475),
            new ComplexNumber(0.01988, 0.0027475),
            new ComplexNumber(0.01988, 0.0027475),
            new ComplexNumber(0.032305,0.0023765),

            new ComplexNumber(9.83040, 0.86719),
            new ComplexNumber(10000000,0),
            new ComplexNumber(10000000,0),

            new ComplexNumber(0.017608,0.0024335),
            new ComplexNumber(0.017608,0.0024335),
            new ComplexNumber(0.017608,0.0024335),
            new ComplexNumber(0.028613,0.0021049),

            new ComplexNumber(39.71879,11.58465),
            new ComplexNumber(10000000,0),
            new ComplexNumber(19.85939,5.79232),

            new ComplexNumber(0.013632,0.001884),
            new ComplexNumber(0.013632,0.001884),
            new ComplexNumber(0.013632,0.001884),
            new ComplexNumber(0.022152,0.0016296),

            new ComplexNumber(19.85939,5.79232),
            new ComplexNumber(19.85939,5.79232),
            new ComplexNumber(19.85939,5.79232)
        };

        private List<ComplexNumber> _branchEVMs = new List<ComplexNumber>
        {
            new ComplexNumber(230.94011, 0),
            new ComplexNumber(-115.47005, -200.00000),
            new ComplexNumber(-115.47005, 200.00000),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0)
        };

        private List<ComplexNumber> _branchCurrentSources = new List<ComplexNumber>
        {
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0),
            new ComplexNumber(0, 0)
        };

        private List<string> _towersNumbers = new List<string> { "№1", "№2", "№2п", "№3" };
        private List<string> _calculationPoints = new List<string> { "№1", "№2", "№2п", "№3" };
    }
}