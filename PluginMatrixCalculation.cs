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
using System.Linq;
using AcAp = Autodesk.AutoCAD.ApplicationServices.Application;
using PluginMatrixCalculation.models.Wires;
using MathNet.Numerics.LinearAlgebra;
using Complex64 = System.Numerics.Complex;

namespace PluginMatrixCalculation
{
    public class Commands : IExtensionApplication
    {
        private string _calculationId = null;

        [CommandMethod("selb")]
        public CalculationCreateModel getCalculationCreateModel()
        {
            List<WireSpan> wireSpans = new List<WireSpan>();
            List<Consumer> consumers = new List<Consumer>();
            List<Pole> poles = new List<Pole>();

            Editor ed = Application.DocumentManager.MdiActiveDocument.Editor;
            Database db = Application.DocumentManager.MdiActiveDocument.Database;
            using (Transaction trans = db.TransactionManager.StartTransaction())
            {
                // получаем таблицу блоков и проходим по всем записям таблицы блоков
                BlockTable bt = trans.GetObject(db.BlockTableId, OpenMode.ForRead) as BlockTable;
                foreach (ObjectId btrId in bt)
                {
                    // получаем запись таблицы блоков и смотри анонимная ли она
                    BlockTableRecord btr = trans.GetObject(btrId, OpenMode.ForRead) as BlockTableRecord;
                    if (btr.IsDynamicBlock)
                    {
                        // получаем все анонимные блоки динамического блока
                        ObjectIdCollection anonymousIds = btr.GetAnonymousBlockIds();
                        // получаем все прямые вставки динамического блока
                        ObjectIdCollection dynBlockRefs = btr.GetBlockReferenceIds(true, true);
                        foreach (ObjectId anonymousBtrId in anonymousIds)
                        {
                            // получаем анонимный блок
                            BlockTableRecord anonymousBtr = trans.GetObject(anonymousBtrId, OpenMode.ForRead) as BlockTableRecord;
                            // получаем все вставки этого блока
                            ObjectIdCollection blockRefIds = anonymousBtr.GetBlockReferenceIds(true, true);
                            foreach (ObjectId id in blockRefIds)
                            {
                                dynBlockRefs.Add(id);
                            }
                        }

                        foreach (ObjectId id in dynBlockRefs)
                        {
                            BlockReference bref = trans.GetObject(id, OpenMode.ForRead) as BlockReference;

                            if (bref.IsDynamicBlock)
                            {
                                List<DynamicBlockReferenceProperty> props = new List<DynamicBlockReferenceProperty>();
                                foreach (DynamicBlockReferenceProperty prop in bref.DynamicBlockReferencePropertyCollection)
                                {
                                    props.Add(prop);
                                }

                                Point firstPoint = new Point(bref.Position.X, bref.Position.Y);
                                if (btr.Name == "Пролет")
                                {
                                    Point secondPoint = firstPoint + new Point(
                                        (double)props.First(p => p.PropertyName == "Положение1 X").Value,
                                        (double)props.First(p => p.PropertyName == "Положение1 Y").Value
                                    );
                                    string wireType = props.First(p => p.PropertyName == "Марка провода").Value as string;
                                    int length = Convert.ToInt32(Math.Round((double)props.First(p => p.PropertyName == "Длина пролета").Value));
                                    wireSpans.Add(
                                        new WireSpan(
                                            firstPoint,
                                            secondPoint,
                                            СИП_2.Values.First(c => c.Name == wireType),
                                            length
                                        )
                                    );
                                }
                                else if (btr.Name == "Опора")
                                {
                                    List<AttributeReference> attrs = new List<AttributeReference>();
                                    foreach (ObjectId propId in bref.AttributeCollection)
                                    {
                                        attrs.Add(trans.GetObject(propId, OpenMode.ForRead) as AttributeReference);
                                    }

                                    string poleNumber = attrs.First(a => a.Tag == "НОМЕР_ОПОРЫ").TextString;

                                    poles.Add(new Pole(firstPoint, poleNumber));
                                }
                                else if (btr.Name == "Абонент")
                                {
                                    string consumerName = props.First(p => p.PropertyName == "Марка провода").Value as string;
                                    consumers.Add(new Consumer(firstPoint, ConsumerType.Values.First(c => c.Name == consumerName)));
                                }
                            }
                        }
                    }
                }

                MatrixBuilder<double> MComplex = Matrix<double>.Build;

                var COM = MComplex.DenseOfArray(new double[,]  {{-1,  0,   0,   0}, //Матрица соеднинения звена
                                                                { 0,  0,   0,   1},
                                                                { 0, -1,   0,   0},
                                                                { 0,  0,  -1,   0}});


                var CON = MComplex.DenseOfArray(new double[,]  {{ 1,  0,  0},       //Матрица потребителя звена
                                                                {-1, -1, -1},
                                                                { 0,  1,  0},
                                                                { 0,  0,  1}});


                int ElementsCount = wireSpans.Count;
                Matrix<double> MConnection = MComplex.Dense(ElementsCount * 4 + 3, ElementsCount * 7 + 3);
                MConnection.SetSubMatrix(0, 3, 0, 3, MComplex.DenseDiagonal(3, 3, -1));
                int u = 0;
                int v = 4;
                var qwe = new { Name = "Abhimanyu", Age = 21 };
                List<WireSpan> wireSpansCopy = new List<WireSpan>(wireSpans);
                Dictionary<WireSpan, (int, Pole)> topology = new Dictionary<WireSpan, (int, Pole)>();
                while (wireSpansCopy.Count > 0)
                {
                    u = u + 1;

                    WireSpan currentWireSpan;
                    if (topology.Count == 0)
                    {
                        MConnection.SetSubMatrix(0, 3, v - 1, 3, -MComplex.DenseDiagonal(3, 3, -1));
                        currentWireSpan = wireSpansCopy.First(ws => poles.All(p => p.Point != ws.Start));
                    } else
                    {
                        currentWireSpan = wireSpansCopy.First(ws => topology.Keys.Any(k => k.End == ws.Start));
                        WireSpan key = topology.Keys.First(k => k.End == currentWireSpan.Start);
                        (int uForConnect, _) = topology[key];
                        MConnection.SetSubMatrix(uForConnect * 4 - 1, 4, v - 1, 4, -COM);
                    }

                    Pole pole = poles.First(p => p.Point == currentWireSpan.End);
                    topology.Add(currentWireSpan, (u, pole));
                    wireSpansCopy.Remove(currentWireSpan);

                    MConnection.SetSubMatrix(u * 4 - 1, 4, v - 1, 4, COM);
                    v = v + 4;
                    MConnection.SetSubMatrix(u * 4 - 1, 4, v - 1, 3, CON);
                    v = v + 3;
                }

                List<WireSpan> wireEndSpans = wireSpans.Where(ws => wireSpans.All(x => ws.End != x.Start)).ToList();
                List<Pole> polesToCalculate = poles.Where(p => wireEndSpans.Any(ws => ws.End == p.Point)).ToList();

                List<List<int>> networkTopology =
                    MConnection.ToRowArrays().Select(x => x.Select(y => Convert.ToInt32(y)).ToList()).ToList();

                List<Complex64> branchResistances = new List<Complex64>
                {
                    new Complex64(0.00947, 0.0272),
                    new Complex64(0.00947, 0.0272),
                    new Complex64(0.00947, 0.0272),
                };

                List<Complex64> accumPowerByPhase = Enumerable.Range(1, 3).Select(_ => new Complex64()).ToList();

                foreach (WireSpan wireSpan in topology.Keys)
				{
                    branchResistances.AddRange(Enumerable.Range(1, 3).Select(_ => wireSpan.Wire.PhaseWireResistivity.ToComplex64() * wireSpan.Length / 1000));
                    branchResistances.Add(wireSpan.Wire.NeutralWireResistivity.ToComplex64() * wireSpan.Length / 1000);

                    List<Consumer> consumersOfWireSpan = consumers.Where(c => c.Point == wireSpan.End).ToList();

                    Complex64 accumPowerOfPhaseA = new Complex64();
                    Complex64 accumPowerOfPhaseB = new Complex64();
                    Complex64 accumPowerOfPhaseC = new Complex64();
                    foreach (Consumer consumer in consumersOfWireSpan)
					{
                        Complex64 currPower = consumer.ConsumerType.Power.ToComplex64();
                        if (consumer.ConsumerType.IsThreePhase)
						{
                            accumPowerByPhase = accumPowerByPhase.Select(p => p + currPower).ToList();
                            accumPowerOfPhaseA = accumPowerOfPhaseA + currPower;
                            accumPowerOfPhaseB = accumPowerOfPhaseB + currPower;
                            accumPowerOfPhaseC = accumPowerOfPhaseC + currPower;
                        } else
						{
                            double powerOfCurrentPhase = accumPowerByPhase.Select(p => p.Magnitude).Min();
                            int index = accumPowerByPhase.FindIndex(p => p.Magnitude == powerOfCurrentPhase);
                            accumPowerByPhase[index] = powerOfCurrentPhase + currPower;
                            if (index == 0)
							{
                                accumPowerOfPhaseA = accumPowerOfPhaseA + currPower;
                            } else if (index == 1)
							{
                                accumPowerOfPhaseB = accumPowerOfPhaseB + currPower;
                            }
                            else if (index == 2)
                            {
                                accumPowerOfPhaseC = accumPowerOfPhaseC + currPower;
                            }
                        }
                    }

                    branchResistances.Add(accumPowerOfPhaseA.Magnitude != 0 ? Complex64.Pow(230.94011, 2) / accumPowerOfPhaseA : Math.Pow(10, 7));
                    branchResistances.Add(accumPowerOfPhaseB.Magnitude != 0 ? Complex64.Pow(230.94011, 2) / accumPowerOfPhaseB : Math.Pow(10, 7));
                    branchResistances.Add(accumPowerOfPhaseC.Magnitude != 0 ? Complex64.Pow(230.94011, 2) / accumPowerOfPhaseC : Math.Pow(10, 7));

                }

                List<ComplexNumber> branchEVMs = new List<ComplexNumber>
                {
                    new ComplexNumber(230.94011, 0),
                    new ComplexNumber(-115.47005, -200.00000),
                    new ComplexNumber(-115.47005, 200.00000),
                };
                branchEVMs.AddRange(Enumerable.Range(1, networkTopology[0].Count - 3).Select(_ => new ComplexNumber(0, 0)));

                return new CalculationCreateModel(
                    networkTopology,
                    branchResistances.Select(br => new ComplexNumber(br)).ToList(),
                    branchEVMs,
                    Enumerable.Range(1, networkTopology[0].Count).Select(_ => new ComplexNumber(0, 0)).ToList(),
                    topology.Values.Select(value => value.Item2.Number).ToList(),
                    polesToCalculate.Select(p => p.Number).ToList()
                );
            }
        }

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

                var jsonSerializerSettings = new JsonSerializerSettings();
                jsonSerializerSettings.ContractResolver = new CamelCasePropertyNamesContractResolver();
                string requestString = JsonConvert.SerializeObject(getCalculationCreateModel(), jsonSerializerSettings);
                
                var stringContent = new StringContent(requestString, Encoding.UTF8, "application/json");
                
                var response = client.PostAsync("http://localhost:8080/api/calculation", stringContent).Result;
                var responseString = response.Content.ReadAsStringAsync().Result;

                CalculationResultsModel results = null;
                try
				{
                    results =
                    JsonConvert.DeserializeObject<CalculationResultsModel>(responseString, jsonSerializerSettings);

                    _calculationId = results.CalculationId;

                    CreateResultTables(db, ed, results);

                    ed.WriteMessage("\nРасчёт параметров электрической сети выполнен");
                } catch (System.Exception error)
				{
                    Console.WriteLine(error);
				}
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
                if (_calculationId == null)
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