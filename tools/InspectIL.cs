using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Reflection.Emit;
using System.Collections.Generic;

// Offline IL inspection: ReflectionOnlyLoad does not execute the target assembly.
class InspectIL
{
    static readonly Dictionary<short, OpCode> Ops = typeof(OpCodes).GetFields()
        .Where(f => f.FieldType == typeof(OpCode)).Select(f => (OpCode)f.GetValue(null))
        .ToDictionary(o => o.Value);
    static string folder;
    static int Main(string[] args)
    {
        try {
            folder = Path.GetDirectoryName(Path.GetFullPath(args[0]));
            AppDomain.CurrentDomain.ReflectionOnlyAssemblyResolve += (s, e) => {
                string p = Path.Combine(folder, new AssemblyName(e.Name).Name + ".dll");
                return File.Exists(p) ? Assembly.ReflectionOnlyLoadFrom(p) : Assembly.ReflectionOnlyLoad(e.Name);
            };
            Assembly a = Assembly.ReflectionOnlyLoadFrom(args[0]);
            Type[] types;
            try { types = a.GetTypes(); }
            catch (ReflectionTypeLoadException e) { types = e.Types.Where(t => t != null).ToArray(); }
            string filter = args.Length > 1 ? args[1] : "Language";
            foreach (Type t in types) {
                MethodBase[] methods;
                try { methods = t.GetMethods(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance|BindingFlags.DeclaredOnly)
                    .Cast<MethodBase>().Concat(t.GetConstructors(BindingFlags.Public|BindingFlags.NonPublic|BindingFlags.Static|BindingFlags.Instance)).ToArray(); }
                catch { continue; }
                foreach (MethodBase m in methods) {
                    if ((t.FullName + "." + m.Name).IndexOf(filter, StringComparison.OrdinalIgnoreCase) < 0) continue;
                    Console.WriteLine("\nMETHOD " + t.FullName + "." + m.Name);
                    MethodBody body;
                    try { body = m.GetMethodBody(); } catch { continue; }
                    if (body == null) continue;
                    byte[] b = body.GetILAsByteArray();
                    for (int i = 0; i < b.Length;) {
                        int offset = i;
                        short k = b[i++]; if (k == 0xfe) k = (short)(0xfe00 | b[i++]);
                        OpCode op; if (!Ops.TryGetValue(k, out op)) break;
                        object val = ""; int n = 0;
                        switch (op.OperandType) {
                            case OperandType.InlineNone: break;
                            case OperandType.ShortInlineI: case OperandType.ShortInlineVar: val = b[i]; n=1; break;
                            case OperandType.ShortInlineBrTarget: val = i+1+(sbyte)b[i]; n=1; break;
                            case OperandType.InlineVar: val = BitConverter.ToUInt16(b,i); n=2; break;
                            case OperandType.InlineI: val = BitConverter.ToInt32(b,i); n=4; break;
                            case OperandType.InlineBrTarget: val=i+4+BitConverter.ToInt32(b,i); n=4; break;
                            case OperandType.InlineI8: val=BitConverter.ToInt64(b,i); n=8; break;
                            case OperandType.ShortInlineR: val=BitConverter.ToSingle(b,i); n=4; break;
                            case OperandType.InlineR: val=BitConverter.ToDouble(b,i); n=8; break;
                            case OperandType.InlineSwitch: n=4+4*BitConverter.ToInt32(b,i); val="switch"; break;
                            default:
                                int token=BitConverter.ToInt32(b,i); n=4;
                                try { val=op.OperandType==OperandType.InlineString ? (object)("\""+m.Module.ResolveString(token)+"\"") : m.Module.ResolveMember(token); }
                                catch { val="token:0x"+token.ToString("x8"); }
                                break;
                        }
                        Console.WriteLine("{0:x4}: {1} {2}",offset,op.Name,val); i+=n;
                    }
                }
            }
            return 0;
        } catch(Exception e) { Console.Error.WriteLine(e); return 1; }
    }
}
