#!/usr/bin/env bash
# Builds docs/report/Autonomous_Taxi_Manager_Final_Report.docx (+ .pdf) from final-report.md.
# Needs pandoc, python3 + python-docx, LibreOffice (for the PDF). Run from the repository root.
set -euo pipefail
R=docs/report; T=tools/report; W=$(mktemp -d)
python3 $T/prep.py $R/final-report.md $W/report.md
pandoc $W/report.md -f markdown+pipe_tables+implicit_figures+raw_attribute -o $R/Autonomous_Taxi_Manager_Final_Report.docx --reference-doc=$T/ref.docx --resource-path=$R
python3 $T/post.py $R/Autonomous_Taxi_Manager_Final_Report.docx
(cd $R && soffice --headless --convert-to pdf Autonomous_Taxi_Manager_Final_Report.docx >/dev/null)
echo "built $R/Autonomous_Taxi_Manager_Final_Report.docx and .pdf"
