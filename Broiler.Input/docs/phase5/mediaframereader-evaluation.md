# MediaFrameReader Evaluation

Media Foundation Source Reader remains the Phase 5 Windows provider because it
fits the no-third-party, DllImport/COM boundary and gives direct control over
format negotiation, buffer ownership, and resource shutdown.

`MediaFrameReader` should be evaluated as a separately named provider only when
Broiler needs multi-source groups, synchronized depth/infrared/color streams, or
WinRT-specific sensor metadata. It should not replace the Source Reader provider
for ordinary webcam capture unless those requirements become first-class.
