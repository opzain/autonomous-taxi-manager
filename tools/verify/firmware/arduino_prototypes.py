# Emulates the Arduino IDE's sketch preprocessing: it inserts a prototype for every function just above the
# first function definition. Types used in those prototypes must be declared before that point, or the IDE
# fails with "'X' does not name a type" (bug B015). Writes the transformed sketch for g++ -fsyntax-only.
import re, sys
s = open(sys.argv[1]).read()
funcs = re.findall(r'^((?:static\s+)?(?:const\s+)?[A-Za-z_][\w:<>]*\*?\s+(?:IRAM_ATTR\s+)?\*?\s*([A-Za-z_]\w*)\s*\([^;{]*\))\s*\{', s, re.M)
protos = [f[0].replace('IRAM_ATTR ', '') + ';' for f in funcs if f[1] not in ('if', 'for', 'while', 'switch')]
first = min(s.find(f[0]) for f in funcs)
open(sys.argv[2], 'w').write('#include <Arduino.h>\n' + s[:first] + '\n'.join(protos) + '\n' + s[first:])
print(f"   {len(protos)} Arduino-style prototypes inserted")
