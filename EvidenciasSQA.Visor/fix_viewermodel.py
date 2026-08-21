import chardet
import os

filepath = r"src\EvidenciasSQA.Wpf\ViewModels\ViewerViewModel.cs"

# Detect encoding
with open(filepath, 'rb') as f:
    raw_data = f.read()
    result = chardet.detect(raw_data)

# Read with proper encoding
with open(filepath, 'r', encoding=result['encoding'] or 'utf-8') as f:
    content = f.read()

lines = content.split('\n')
new_lines = []
skip_duplicate = False

for i, line in enumerate(lines):
    if 'public void ResetViewState()' in line and not skip_duplicate:
        new_lines.append(line)
        skip_duplicate = True
    elif 'public void ResetViewState()' in line and skip_duplicate:
        # Skip this duplicate
        continue
    else:
        new_lines.append(line)

with open(filepath, 'w', encoding='utf-8') as f:
    f.write('\n'.join(new_lines))

print('Done removing duplicate ResetViewState')