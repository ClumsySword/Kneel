// Body for UnityMCP execute_code. Snapshot a model's transforms + bindposes before replacing it, then verify
// the replacement against that snapshot. Edit the three constants. C# 6 (CodeDom) compatible.
// "snapshot" before copying the new FBX in; "verify" after the reimport. Verify passes when bad=0,
// maxPos/maxScale ~1e-5, maxRotDeg ~0, bpNameMismatch=0, bpMaxDiff ~1e-5 and the avatar (if any) is valid.
const string MODE = "snapshot";                                   // EDIT: "snapshot" or "verify"
const string MODEL = "Assets/Kneel/Player/Models/EmberWarden/Player_EmberWarden.fbx";   // EDIT
const string SNAP = "C:/path/to/scratch/rig_snapshot.txt";        // EDIT: a scratch file, not in Assets
var ci = System.Globalization.CultureInfo.InvariantCulture;
if (MODE == "verify") AssetDatabase.ImportAsset(MODEL, ImportAssetOptions.ForceUpdate | ImportAssetOptions.ForceSynchronousImport);
var go = AssetDatabase.LoadAssetAtPath<GameObject>(MODEL);
var smr = go.GetComponentInChildren<SkinnedMeshRenderer>(true);
string av = "none";
foreach (var a in AssetDatabase.LoadAllAssetsAtPath(MODEL)) { var x = a as Avatar; if (x != null) av = x.name + " valid=" + x.isValid + " human=" + x.isHuman; }
Mesh m = smr != null ? smr.sharedMesh : go.GetComponentInChildren<MeshFilter>(true).sharedMesh;
string info = "verts=" + m.vertexCount + " tris=" + (m.triangles.Length / 3) + " idx=" + m.indexFormat + " sub=" + m.subMeshCount + " avatar=" + av;
if (MODE == "snapshot") {
  var sb = new System.Text.StringBuilder();
  foreach (var t in go.GetComponentsInChildren<Transform>(true))
    sb.AppendFormat(ci, "T|{0}|{1:R},{2:R},{3:R}|{4:R},{5:R},{6:R},{7:R}|{8:R},{9:R},{10:R}\n", AnimationUtility.CalculateTransformPath(t, go.transform),
      t.localPosition.x, t.localPosition.y, t.localPosition.z, t.localRotation.x, t.localRotation.y, t.localRotation.z, t.localRotation.w, t.localScale.x, t.localScale.y, t.localScale.z);
  if (smr != null) { var bp = m.bindposes; for (int i = 0; i < bp.Length; i++) { sb.Append("B|" + smr.bones[i].name); for (int k = 0; k < 16; k++) sb.Append("|" + bp[i][k].ToString("R", ci)); sb.Append("\n"); } }
  System.IO.File.WriteAllText(SNAP, sb.ToString());
  return "snapshot written: " + info + (smr != null ? " bones=" + smr.bones.Length : "");
}
var oldT = new System.Collections.Generic.Dictionary<string, string[]>(); var oldB = new System.Collections.Generic.List<string[]>();
foreach (var line in System.IO.File.ReadAllLines(SNAP)) { var p = line.Split('|'); if (p[0] == "T") oldT[p[1]] = p; else if (p[0] == "B") oldB.Add(p); }
int n = 0, bad = 0; float maxP = 0, maxR = 0, maxS = 0; var rep = new System.Text.StringBuilder();
foreach (var t in go.GetComponentsInChildren<Transform>(true)) {
  string path = AnimationUtility.CalculateTransformPath(t, go.transform); n++;
  string[] o; if (!oldT.TryGetValue(path, out o)) { rep.Append("NEW " + path + "\n"); bad++; continue; }
  oldT.Remove(path);
  var op = System.Array.ConvertAll(o[2].Split(','), s => float.Parse(s, ci)); var orr = System.Array.ConvertAll(o[3].Split(','), s => float.Parse(s, ci)); var os = System.Array.ConvertAll(o[4].Split(','), s => float.Parse(s, ci));
  maxP = Mathf.Max(maxP, (t.localPosition - new Vector3(op[0], op[1], op[2])).magnitude);
  maxR = Mathf.Max(maxR, Quaternion.Angle(t.localRotation, new Quaternion(orr[0], orr[1], orr[2], orr[3])));
  maxS = Mathf.Max(maxS, (t.localScale - new Vector3(os[0], os[1], os[2])).magnitude);
}
foreach (var k in oldT.Keys) { rep.Append("MISSING " + k + "\n"); bad++; }
string bpRep = "";
if (smr != null) {
  int bpBad = 0; float bpMax = 0; var bp = m.bindposes;
  if (bp.Length != oldB.Count) bpBad = -1;
  else for (int i = 0; i < bp.Length; i++) { if (oldB[i][1] != smr.bones[i].name) bpBad++; for (int k = 0; k < 16; k++) bpMax = Mathf.Max(bpMax, Mathf.Abs(bp[i][k] - float.Parse(oldB[i][k + 2], ci))); }
  int maxBpv = 0; foreach (var b in m.GetBonesPerVertex()) maxBpv = Mathf.Max(maxBpv, b);
  bpRep = " bones=" + smr.bones.Length + " bindposes=" + bp.Length + " bpNameMismatch=" + bpBad + " bpMaxDiff=" + bpMax + " maxBonesPerVertex=" + maxBpv;
}
return "transforms=" + n + " bad=" + bad + " maxPos=" + maxP + " maxRotDeg=" + maxR + " maxScale=" + maxS + "\n" + rep + info + bpRep;
