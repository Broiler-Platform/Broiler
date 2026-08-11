# How much of a real document is an independent subtree

- Source: `tests/wpt`, 188 documents rendered at 1280x1024
- Every figure is a count. No clock, so no warm-up, no median and nothing to drift.

## The distribution

| quantity | min | median | p90 | max |
|---|---:|---:|---:|---:|
| document bytes | 45.0 | 1116.0 | 3581.0 | 11428.0 |
| boxes in the tree | 5.0 | 17.0 | 68.0 | 328.0 |
| tree depth | 4.0 | 5.0 | 7.0 | 10.0 |
| independent share % | 0.0 | 0.0 | 41.2 | 93.4 |

**112 of 188 documents (59.6%) contain no independent subtree at all**, 
and pooled over every box in every document the independent share is 
**18.6%** (1240 of 6669).

## Per directory

Published separately so a reader can see whether one directory carries the pooled
figure — a directory of flexbox tests would report a flex share that says more about
the directory than about documents.

| directory | docs | median boxes | median depth | independent share (pooled) | abspos | cells | flex/grid |
|---|---:|---:|---:|---:|---:|---:|---:|
| css/CSS2 | 1 | 10.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/abspos | 1 | 35.0 | 7.0 | 5.7% | 2 | 0 | 0 |
| css/CSS2/floats-clear | 2 | 20.0 | 8.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/linebox | 1 | 40.0 | 6.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/normal-flow | 1 | 12.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/other-formats | 1 | 73.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/pagination | 2 | 53.0 | 6.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/selector | 1 | 9.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/visudet | 6 | 29.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/CSS2/visufx | 1 | 30.0 | 8.0 | 43.3% | 2 | 0 | 0 |
| css/css-align/abspos | 14 | 68.0 | 7.0 | 41.2% | 196 | 0 | 0 |
| css/css-align/blocks | 15 | 104.0 | 8.0 | 11.5% | 85 | 30 | 0 |
| css/css-align/self-alignment | 2 | 302.0 | 8.0 | 91.0% | 16 | 0 | 96 |
| css/css-anchor-position | 44 | 19.0 | 6.0 | 18.4% | 105 | 0 | 17 |
| css/css-anchor-position/reference | 9 | 11.0 | 5.0 | 9.9% | 9 | 0 | 0 |
| css/css-animations | 1 | 17.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/css-backgrounds | 19 | 12.0 | 5.0 | 0.8% | 2 | 0 | 0 |
| css/css-backgrounds/animations | 6 | 12.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/css-backgrounds/background-clip | 24 | 25.0 | 6.0 | 1.4% | 0 | 4 | 0 |
| css/css-backgrounds/background-size/vector | 27 | 16.0 | 5.0 | 0.0% | 0 | 0 | 0 |
| css/css-backgrounds/background-size/vector/reference | 8 | 13.0 | 6.0 | 0.0% | 0 | 0 | 0 |
| css/reference | 2 | 15.0 | 6.0 | 0.0% | 0 | 0 | 0 |

## The ten largest trees

The tail is where a subtree split would have anything to divide, so it is published
rather than summarised away.

| document | bytes | boxes | depth | independent | share |
|---|---:|---:|---:|---:|---:|
| css/css-align/blocks/align-content-block-006.html | 10218 | 328 | 9 | 34 | 10.4% |
| css/css-align/blocks/align-content-block-008.html | 11428 | 328 | 9 | 34 | 10.4% |
| css/css-align/blocks/align-content-block-010.html | 10221 | 328 | 9 | 34 | 10.4% |
| css/css-align/blocks/align-content-block-004.html | 9661 | 324 | 9 | 34 | 10.5% |
| css/css-align/self-alignment/block-justify-self.html | 10059 | 302 | 8 | 282 | 93.4% |
| css/css-align/blocks/align-content-block-002.html | 8510 | 258 | 9 | 34 | 13.2% |
| css/css-align/blocks/align-content-block-break-overflow-010-ref.html | 3474 | 174 | 8 | 0 | 0.0% |
| css/css-align/blocks/align-content-block-break-overflow-010.html | 3865 | 158 | 10 | 0 | 0.0% |
| css/css-align/self-alignment/block-justify-self-ref.html | 3646 | 111 | 8 | 94 | 84.7% |
| css/css-align/blocks/safe-justify-self-vrl.html | 4145 | 104 | 6 | 0 | 0.0% |

