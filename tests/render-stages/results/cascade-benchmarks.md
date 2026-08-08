# Cascade benchmarks (BenchmarkDotNet, ShortRun)

```
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --filter '*CascadeBenchmarks*' --job short
```

| Method                                        | Page  | Mean                  | Error                 | StdDev             | Median                | Ratio | RatioSD | Gen0        | Gen1      | Allocated    | Alloc Ratio |
|---------------------------------------------- |------ |----------------------:|----------------------:|-------------------:|----------------------:|------:|--------:|------------:|----------:|-------------:|------------:|
| 'element count (control, not a timing)'       | boxes |             0.0004 ns |             0.0133 ns |          0.0007 ns |             0.0000 ns | 0.000 |    0.00 |           - |         - |            - |        0.00 |
| 'cold cascade over every element'             | boxes |   101,711,030.6667 ns |    87,787,678.5057 ns |  4,811,938.9046 ns |   103,676,626.8750 ns | 1.002 |    0.06 |   3000.0000 | 1000.0000 |   53391410 B |        1.00 |
| 'warm cascade (cache hit) over every element' | boxes |       139,237.1074 ns |       185,632.3427 ns |     10,175.1351 ns |       133,956.1700 ns | 0.001 |    0.00 |           - |         - |            - |        0.00 |
|                                               |       |                       |                       |                    |                       |       |         |             |           |              |             |
| 'element count (control, not a timing)'       | mixed |             0.0013 ns |             0.0304 ns |          0.0017 ns |             0.0008 ns | 0.000 |    0.00 |           - |         - |            - |        0.00 |
| 'cold cascade over every element'             | mixed |    50,957,988.6944 ns |   100,367,930.0677 ns |  5,501,504.9457 ns |    47,833,809.9167 ns | 1.007 |    0.13 |   1583.3333 |  416.6667 |   27368057 B |        1.00 |
| 'warm cascade (cache hit) over every element' | mixed |        59,668.8197 ns |        24,737.9325 ns |      1,355.9696 ns |        59,145.2963 ns | 0.001 |    0.00 |           - |         - |            - |        0.00 |
|                                               |       |                       |                       |                    |                       |       |         |             |           |              |             |
| 'element count (control, not a timing)'       | paint |             0.0052 ns |             0.1636 ns |          0.0090 ns |             0.0000 ns | 0.000 |    0.00 |           - |         - |            - |        0.00 |
| 'cold cascade over every element'             | paint |    95,133,450.1667 ns |   494,627,474.0981 ns | 27,112,201.0107 ns |   109,104,797.3750 ns | 1.069 |    0.42 |   2625.0000 | 1000.0000 |   46742842 B |        1.00 |
| 'warm cascade (cache hit) over every element' | paint |       102,255.6676 ns |        49,564.0968 ns |      2,716.7754 ns |       102,185.8528 ns | 0.001 |    0.00 |           - |         - |            - |        0.00 |
|                                               |       |                       |                       |                    |                       |       |         |             |           |              |             |
| 'element count (control, not a timing)'       | rules |             0.0050 ns |             0.0900 ns |          0.0049 ns |             0.0052 ns | 0.000 |    0.00 |           - |         - |            - |        0.00 |
| 'cold cascade over every element'             | rules | 3,675,250,715.3333 ns | 1,241,070,329.0655 ns | 68,027,252.8156 ns | 3,686,788,122.0000 ns | 1.000 |    0.02 | 361000.0000 | 7000.0000 | 6238123848 B |        1.00 |
| 'warm cascade (cache hit) over every element' | rules |       346,259.6156 ns |       240,203.2669 ns |     13,166.3516 ns |       341,744.5781 ns | 0.000 |    0.00 |           - |         - |            - |        0.00 |
|                                               |       |                       |                       |                    |                       |       |         |             |           |              |             |
| 'element count (control, not a timing)'       | text  |             0.0244 ns |             0.0905 ns |          0.0050 ns |             0.0238 ns | 0.000 |    0.00 |           - |         - |            - |        0.00 |
| 'cold cascade over every element'             | text  |    20,636,375.0208 ns |    68,534,025.6275 ns |  3,756,581.2176 ns |    18,698,501.0938 ns | 1.020 |    0.22 |    656.2500 |  218.7500 |   11753598 B |        1.00 |
| 'warm cascade (cache hit) over every element' | text  |        39,758.2110 ns |        16,355.3973 ns |        896.4945 ns |        39,495.9138 ns | 0.002 |    0.00 |           - |         - |            - |        0.00 |

# Rule-count scaling (element set and matched rules held fixed)

```
dotnet run -c Release --project tests/render-stages/Broiler.Render.Stage.Benchmarks -- --filter '*RuleScaling*' --job short
```

| Method                                        | RuleCount | Mean       | Error       | StdDev      | Gen0        | Gen1      | Allocated  |
|---------------------------------------------- |---------- |-----------:|------------:|------------:|------------:|----------:|-----------:|
| 'cold cascade, fixed elements, growing sheet' | 100       |   114.9 ms |    229.2 ms |    12.56 ms |  11000.0000 | 1000.0000 |   181.7 MB |
| 'cold cascade, fixed elements, growing sheet' | 400       |   466.5 ms |    579.8 ms |    31.78 ms |  43000.0000 | 1000.0000 |  721.96 MB |
| 'cold cascade, fixed elements, growing sheet' | 1600      | 2,532.2 ms | 22,195.4 ms | 1,216.61 ms | 176000.0000 | 3000.0000 | 2897.53 MB |
| 'cold cascade, fixed elements, growing sheet' | 3200      | 3,543.6 ms |    921.1 ms |    50.49 ms | 353000.0000 | 3000.0000 | 5817.94 MB |
