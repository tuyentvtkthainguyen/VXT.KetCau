// Giả lập chữ ký CSi OAPI (theo tài liệu CSi API ETABS 22 / SAP2000 v25) — chỉ để biên dịch trên CI.
#pragma warning disable
using System;
namespace CSiAPIv1
{
    public enum eUnits { lb_in_F = 1, lb_ft_F, kip_in_F, kip_ft_F, kN_mm_C, kN_m_C, kgf_mm_C, kgf_m_C, N_mm_C, N_m_C, Ton_mm_C, Ton_m_C, kN_cm_C, kgf_cm_C, N_cm_C, Ton_cm_C }
    public enum eMatType { Steel = 1, Concrete, NoDesign, Aluminum, ColdFormed, Rebar, Tendon }
    public enum eSlabType { Slab = 0, Drop, Stiff, Ribbed, Waffle, Mat, Footing }
    public enum eShellType { ShellThin = 1, ShellThick, Membrane, Layered }
    public enum eLoadPatternType { Dead = 1, SuperDead, Live, ReduceLive, Quake, Wind, Snow, Other }
    public enum eDiaphragmOption { Disconnect = 1, FromShellObject, DefinedDiaphragm }
    public enum eCNameType { LoadCase = 0, LoadCombo }
    public enum eItemType { Objects = 0, Group, SelectedObjects }
    public enum eItemTypeElm { ObjectElm = 0, Element, GroupElm, SelectionElm }

    public interface cHelper
    {
        cOAPI GetObject(string typeName);
        cOAPI CreateObjectProgID(string progId);
        cOAPI CreateObject(string fullPath);
    }
    public class Helper : cHelper
    {
        public cOAPI GetObject(string typeName) => throw new NotSupportedException();
        public cOAPI CreateObjectProgID(string progId) => throw new NotSupportedException();
        public cOAPI CreateObject(string fullPath) => throw new NotSupportedException();
    }
    public interface cOAPI
    {
        int ApplicationStart();
        int ApplicationExit(bool fileSave);
        cSapModel SapModel { get; }
    }
    public interface cSapModel
    {
        int InitializeNewModel(eUnits units = eUnits.kip_in_F);
        int SetPresentUnits(eUnits units);
        cFile File { get; }
        cFrameObj FrameObj { get; }
        cAreaObj AreaObj { get; }
        cPointObj PointObj { get; }
        cDiaphragm Diaphragm { get; }
        cPropMaterial PropMaterial { get; }
        cPropFrame PropFrame { get; }
        cPropArea PropArea { get; }
        cStory Story { get; }
        cLoadPatterns LoadPatterns { get; }
        cFunction Func { get; }
        cMassSource SourceMass { get; }
        cLoadCases LoadCases { get; }
        cCombo RespCombo { get; }
        cAnalyze Analyze { get; }
        cAnalysisResults Results { get; }
        cView View { get; }
        cDesignConcrete DesignConcrete { get; }
    }
    public interface cFile { int NewBlank(); int Save(string fileName = ""); }
    public interface cView { int RefreshView(int window = 0, bool zoom = true); }
    public interface cFrameObj
    {
        int AddByCoord(double xi, double yi, double zi, double xj, double yj, double zj, ref string name, string propName = "Default", string userName = "", string cSys = "Global");
        int SetLocalAxes(string name, double ang, eItemType itemType = eItemType.Objects);
        int GetPoints(string name, ref string point1, ref string point2);
        int SetInsertionPoint(string name, int cardinalPoint, bool mirror2, bool stiffTransform, ref double[] offset1, ref double[] offset2, string cSys = "Local", eItemType itemType = eItemType.Objects);
        int SetLoadDistributed(string name, string loadPat, int myType, int dir, double dist1, double dist2, double val1, double val2, string cSys = "Global", bool relDist = true, bool replace = true, eItemType itemType = eItemType.Objects);
        int GetNameList(ref int numberNames, ref string[] myName);
        int GetSection(string name, ref string propName, ref string sAuto);
        int GetLocalAxes(string name, ref double ang, ref bool advanced);
    }
    public interface cAreaObj
    {
        int AddByCoord(int numberPoints, ref double[] x, ref double[] y, ref double[] z, ref string name, string propName = "Default", string userName = "", string cSys = "Global");
        int SetDiaphragm(string name, string diaphragmName, eItemType itemType = eItemType.Objects);
        int SetAutoMesh(string name, int meshType, int n1 = 2, int n2 = 2, double maxSize1 = 0, double maxSize2 = 0, bool pointOnEdgeFromLine = false, bool pointOnEdgeFromPoint = false, bool extendCookieCutLines = false, double rotation = 0, double maxSizeGeneral = 0, bool localAxesOnEdge = false, bool localAxesOnFace = false, bool restraintsOnEdge = false, bool restraintsOnFace = false, string group = "ALL", bool subMesh = false, double subMeshSize = 0, eItemType itemType = eItemType.Objects);
        int SetLoadUniform(string name, string loadPat, double value, int dir, bool replace = true, string cSys = "Global", eItemType itemType = eItemType.Objects);
        int GetNameList(ref int numberNames, ref string[] myName);
        int GetPoints(string name, ref int numberPoints, ref string[] point);
        int GetProperty(string name, ref string propName);
    }
    public interface cPointObj
    {
        int SetRestraint(string name, ref bool[] value, eItemType itemType = eItemType.Objects);
        int AddCartesian(double x, double y, double z, ref string name, string userName = "", string cSys = "Global", bool mergeOff = false, int mergeNumber = 0);
        int SetDiaphragm(string name, eDiaphragmOption diaphragmOption, string diaphragmName = "");
        int SetLoadForce(string name, string loadPat, ref double[] value, bool replace = false, string cSys = "Global", eItemType itemType = eItemType.Objects);
        int GetCoordCartesian(string name, ref double x, ref double y, ref double z, string cSys = "Global");
    }
    public interface cDiaphragm { int SetDiaphragm(string name, bool semiRigid); }
    public interface cPropMaterial
    {
        int SetMaterial(string name, eMatType matType, int color = -1, string notes = "", string guid = "");
        int SetMPIsotropic(string name, double e, double u, double a, double temp = 0);
        int SetMPUniaxial(string name, double e, double a, double temp = 0);
        int SetWeightAndMass(string name, int myOption, double value, double temp = 0);
        int SetOConcrete_1(string name, double fc, bool isLightweight, double fcsFactor, int ssType, int ssHysType, double strainAtFc, double strainUltimate, double finalSlope, double frictionAngle = 0, double dilatationalAngle = 0, double temp = 0);
        int SetORebar_1(string name, double fy, double fu, double eFy, double eFu, int ssType, int ssHysType, double strainAtHardening, double strainUltimate, double finalSlope, bool useCaltransSSDefaults, double temp = 0);
    }
    public interface cPropFrame
    {
        int SetRectangle(string name, string matProp, double t3, double t2, int color = -1, string notes = "", string guid = "");
        int SetRebarBeam(string name, string matPropLong, string matPropConfine, double coverTop, double coverBot, double topLeftArea, double topRightArea, double botLeftArea, double botRightArea);
        int SetRebarColumn(string name, string matPropLong, string matPropConfine, int pattern, int confineType, double cover, int numberCBars, int numberR3Bars, int numberR2Bars, string rebarSize, string tieSize, double tieSpacingLongit, int number2DirTieBars, int number3DirTieBars, bool toBeDesigned);
        int SetModifiers(string name, ref double[] value);
        int GetRectangle(string name, ref string fileName, ref string matProp, ref double t3, ref double t2, ref int color, ref string notes, ref string guid);
    }
    public interface cPropArea
    {
        int SetSlab(string name, eSlabType slabType, eShellType shellType, string matProp, double thickness, int color = -1, string notes = "", string guid = "");
        int SetShell_1(string name, int shellType, bool includeDrillingDOF, string matProp, double matAng, double thickness, double bending, int color = -1, string notes = "", string guid = "");
        int GetSlab(string name, ref eSlabType slabType, ref eShellType shellType, ref string matProp, ref double thickness, ref int color, ref string notes, ref string guid);
    }
    public interface cStory
    {
        int SetStories_2(double baseElevation, int numberStories, ref string[] storyNames, ref double[] storyHeights, ref bool[] isMasterStory, ref string[] similarToStory, ref bool[] spliceAbove, ref double[] spliceHeight, ref int[] color);
    }
    public interface cLoadPatterns { int Add(string name, eLoadPatternType myType, double selfWTMultiplier = 0, bool addAnalysisCase = true); }
    public interface cFunction { cFunctionRS FuncRS { get; } }
    public interface cFunctionRS { int SetUser(string name, int numberItems, ref double[] period, ref double[] value, double dampRatio); }
    public interface cMassSource { int SetMassSource(string name, bool massFromElements, bool massFromMasses, bool massFromLoads, bool isDefault, int numberLoads, ref string[] loadPat, ref double[] sf); }
    public interface cLoadCases { cCaseResponseSpectrum ResponseSpectrum { get; } }
    public interface cCaseResponseSpectrum
    {
        int SetCase(string name);
        int SetLoads(string name, int numberLoads, ref string[] loadName, ref string[] func, ref double[] sf, ref string[] cSys, ref double[] ang);
    }
    public interface cCombo
    {
        int Add(string name, int comboType);
        int SetCaseList(string name, ref eCNameType cNameType, string cName, double sf);
        int GetNameList(ref int numberNames, ref string[] myName);
    }
    public interface cDesignConcrete
    {
        int GetSummaryResultsBeam(string name, ref int numberItems, ref string[] frameName, ref double[] location, ref string[] topCombo, ref double[] topArea, ref string[] botCombo, ref double[] botArea, ref string[] vMajorCombo, ref double[] vMajorArea, ref string[] tlCombo, ref double[] tlArea, ref string[] ttCombo, ref double[] ttArea, ref string[] errorSummary, ref string[] warningSummary, eItemType itemType = eItemType.Objects);
        int GetSummaryResultsColumn(string name, ref int numberItems, ref string[] frameName, ref int[] myOption, ref double[] location, ref string[] pmmCombo, ref double[] pmmArea, ref double[] pmmRatio, ref string[] vMajorCombo, ref double[] avMajor, ref string[] vMinorCombo, ref double[] avMinor, ref string[] errorSummary, ref string[] warningSummary, eItemType itemType = eItemType.Objects);
    }
    public interface cAnalyze { int RunAnalysis(); }
    public interface cAnalysisResultsSetup { int DeselectAllCasesAndCombosForOutput(); int SetComboSelectedForOutput(string name, bool selected = true); }
    public interface cAnalysisResults
    {
        cAnalysisResultsSetup Setup { get; }
        int FrameForce(string name, eItemTypeElm itemTypeElm, ref int numberResults, ref string[] obj, ref double[] objSta, ref string[] elm, ref double[] elmSta, ref string[] loadCase, ref string[] stepType, ref double[] stepNum, ref double[] p, ref double[] v2, ref double[] v3, ref double[] t, ref double[] m2, ref double[] m3);
    }
}
