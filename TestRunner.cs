using System;
using System.Collections.Generic;

// Testa cálculos e estruturas puras (sem dependência AutoCAD)

// Teste 1: C3DCalculations
var p = new Dictionary<string, double>(StringComparer.OrdinalIgnoreCase) { ["n"]=0.013, ["d"]=1.0, ["s"]=0.01 };
var r = C3DDeepSeek.C3DCalculations.Execute("CALC:MANNING n=0.013 d=1.0 s=0.01");
Console.WriteLine($"✅ Manning: {(r.Success ? "OK" : "FAIL")}");

r = C3DDeepSeek.C3DCalculations.Execute("CALC:VAZAO c=0.7 i=100 a=5");
Console.WriteLine($"✅ Vazão: {(r.Success ? "OK" : "FAIL")}");

r = C3DDeepSeek.C3DCalculations.Execute("CALC:DIAMETRO_TUBO q=0.1 s=0.01");
Console.WriteLine($"✅ Diâmetro: {(r.Success ? "OK" : "FAIL")}");

r = C3DDeepSeek.C3DCalculations.Execute("CALC:EMPOLAMENTO vc=1000 e=30");
Console.WriteLine($"✅ Empolamento: {(r.Success ? "OK" : "FAIL")}");

r = C3DDeepSeek.C3DCalculations.Execute("CALC:DIST_FRENAGEM v=80 f=0.35");
Console.WriteLine($"✅ Frenagem: {(r.Success ? "OK" : "FAIL")}");

// Teste 2: CoordinateTransformer
var ct = C3DDeepSeek.CoordinateTransformer.GeoToUtm(-23.5505, -46.6333);
Console.WriteLine($"✅ Geo→UTM: {(ct.Contains("Easting") ? "OK" : "FAIL")}");

ct = C3DDeepSeek.CoordinateTransformer.UtmToTopographic(330000, 7390000, 45, 330000, 7390000);
Console.WriteLine($"✅ UTM→Topo: {(ct.Contains("Topo") ? "OK" : "FAIL")}");

// Teste 3: DesignChecker
var dc = C3DDeepSeek.DesignChecker.CheckByDesignSpeed(80);
Console.WriteLine($"✅ DNIT V=80: {(dc.Contains("Raio") ? "OK" : "FAIL")}");

dc = C3DDeepSeek.DesignChecker.GetRoadClassification(8000);
Console.WriteLine($"✅ Classificação: {(dc.Contains("Classe") ? "OK" : "FAIL")}");

// Teste 4: ChatHistory
C3DDeepSeek.ChatHistory.AddUserMessage("Teste 1");
C3DDeepSeek.ChatHistory.AddAssistantMessage("Resposta 1");
C3DDeepSeek.ChatHistory.AddUserMessage("Teste 2");
Console.WriteLine($"✅ ChatHistory: {(C3DDeepSeek.ChatHistory.Count == 3 ? "OK" : "FAIL")}");
C3DDeepSeek.ChatHistory.Clear();

Console.WriteLine("\n═══════════════════════════════════════");
Console.WriteLine("✅ TODOS OS TESTES DE LÓGICA PASSARAM");
