// Strongly typed Windows UIA COM bridge. The older managed UIAutomation client can
// return only Win32 proxies on current Windows builds, omitting WinForms providers.
// Interop is generated locally from the installed Windows type library, never shipped.
using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows;
using System.Windows.Automation;
using NativeUIA;

public sealed class FighterNativeElement
{
    private static readonly IUIAutomation Client = new CUIAutomationClass();
    internal readonly IUIAutomationElement Element;
    internal FighterNativeElement(IUIAutomationElement element) { Element=element; }
    public static FighterNativeElement RootElement { get { return new FighterNativeElement(Client.GetRootElement()); } }
    public static FighterNativeElement FromHandle(IntPtr handle) { var element=Client.ElementFromHandle(handle);return element==null?null:new FighterNativeElement(element); }
    public static AutomationProperty ProcessIdProperty=AutomationElement.ProcessIdProperty,
        NameProperty=AutomationElement.NameProperty, AutomationIdProperty=AutomationElement.AutomationIdProperty,
        ControlTypeProperty=AutomationElement.ControlTypeProperty, ClassNameProperty=AutomationElement.ClassNameProperty;
    private static IUIAutomationCondition ConvertCondition(Condition condition)
    {
        var property=condition as PropertyCondition;
        if(property!=null) return Client.CreatePropertyCondition(property.Property.Id,property.Value is ControlType ? ((ControlType)property.Value).Id : property.Value);
        var and=condition as AndCondition;
        if(and!=null) return and.GetConditions().Select(ConvertCondition).Aggregate((a,b)=>Client.CreateAndCondition(a,b));
        var or=condition as OrCondition;
        if(or!=null) return or.GetConditions().Select(ConvertCondition).Aggregate((a,b)=>Client.CreateOrCondition(a,b));
        if(condition==Condition.TrueCondition) return Client.CreateTrueCondition();
        throw new NotSupportedException("Unsupported test condition.");
    }
    internal static FighterNativeElement[] Wrap(IUIAutomationElementArray array)
    {
        var result=new List<FighterNativeElement>();
        if(array!=null) for(int i=0;i<array.Length;i++) result.Add(new FighterNativeElement(array.GetElement(i)));
        return result.ToArray();
    }
    public FighterNativeElement FindFirst(System.Windows.Automation.TreeScope scope,Condition condition)
    { var result=Element.FindFirst((NativeUIA.TreeScope)(int)scope,ConvertCondition(condition)); return result==null?null:new FighterNativeElement(result); }
    public FighterNativeElement[] FindAll(System.Windows.Automation.TreeScope scope,Condition condition)
    { return Wrap(Element.FindAll((NativeUIA.TreeScope)(int)scope,ConvertCondition(condition))); }
    public FighterNativeInfo Current { get { return Element==null?null:new FighterNativeInfo(Element); } }
    public void SetFocus() { Element.SetFocus(); }
    public FighterNativePattern GetNativePattern(int id)
    { object value=Element.GetCurrentPattern(id); if(value==null)throw new InvalidOperationException("Missing native UIA pattern "+id);return new FighterNativePattern(value,id); }
    public FighterNativePattern GetCurrentPattern(AutomationPattern pattern)
    { object value=Element.GetCurrentPattern(pattern.Id); if(value==null) throw new InvalidOperationException("Missing native UIA pattern "+pattern.ProgrammaticName); return new FighterNativePattern(value,pattern.Id); }
    public bool TryGetCurrentPattern(AutomationPattern pattern,out object result)
    { object value=Element.GetCurrentPattern(pattern.Id); result=value==null?null:new FighterNativePattern(value,pattern.Id); return value!=null; }
}
public sealed class FighterNativeInfo
{
    private readonly IUIAutomationElement e;
    internal FighterNativeInfo(IUIAutomationElement value) { e=value; }
    public string Name { get { return e.CurrentName; } }
    public string AutomationId { get { return e.CurrentAutomationId; } }
    public string ClassName { get { return e.CurrentClassName; } }
    public int ProcessId { get { return e.CurrentProcessId; } }
    public int NativeWindowHandle { get { return e.CurrentNativeWindowHandle.ToInt32(); } }
    public bool IsOffscreen { get { return e.CurrentIsOffscreen!=0; } }
    public bool IsEnabled { get { return e.CurrentIsEnabled!=0; } }
    public bool HasKeyboardFocus { get { return e.CurrentHasKeyboardFocus!=0; } }
    public ControlType ControlType { get { return ControlType.LookupById(e.CurrentControlType); } }
    public Rect BoundingRectangle { get { var r=e.CurrentBoundingRectangle; return new Rect(r.left,r.top,r.right-r.left,r.bottom-r.top); } }
}
public sealed class FighterNativePattern
{
    private readonly object value; private readonly int id;
    internal FighterNativePattern(object v,int pattern) { value=v; id=pattern; }
    public FighterNativePattern Current { get { return this; } }
    public void Invoke() { ((IUIAutomationInvokePattern)value).Invoke(); }
    public void Expand() { ((IUIAutomationExpandCollapsePattern)value).Expand(); }
    public void Collapse() { ((IUIAutomationExpandCollapsePattern)value).Collapse(); }
    public int ExpandCollapseState { get { return (int)((IUIAutomationExpandCollapsePattern)value).CurrentExpandCollapseState; } }
    public void Toggle() { ((IUIAutomationTogglePattern)value).Toggle(); }
    public void Close() { ((IUIAutomationWindowPattern)value).Close(); }
    public void Select() { ((IUIAutomationSelectionItemPattern)value).Select(); }
    public void ScrollIntoView() { ((IUIAutomationScrollItemPattern)value).ScrollIntoView(); }
    public void SetValue(string text) { ((IUIAutomationValuePattern)value).SetValue(text); }
    public object Value { get { return id==RangeValuePattern.Pattern.Id ? (object)((IUIAutomationRangeValuePattern)value).CurrentValue : ((IUIAutomationValuePattern)value).CurrentValue; } }
    public double Maximum { get { return ((IUIAutomationRangeValuePattern)value).CurrentMaximum; } }
    public bool IsReadOnly { get { return ((IUIAutomationValuePattern)value).CurrentIsReadOnly!=0; } }
    public bool CanMove { get { return ((IUIAutomationTransformPattern)value).CurrentCanMove!=0; } }
    public bool CanResize { get { return ((IUIAutomationTransformPattern)value).CurrentCanResize!=0; } }
    public int ToggleState { get { return (int)((IUIAutomationTogglePattern)value).CurrentToggleState; } }
    public uint LegacyState { get { return ((IUIAutomationLegacyIAccessiblePattern)value).CurrentState; } }
    public FighterNativeElement[] GetSelection() { return FighterNativeElement.Wrap(((IUIAutomationSelectionPattern)value).GetCurrentSelection()); }
    public void SetWindowVisualState(System.Windows.Automation.WindowVisualState state) { ((IUIAutomationWindowPattern)value).SetWindowVisualState((NativeUIA.WindowVisualState)(int)state); }
    public void Move(double x,double y) { ((IUIAutomationTransformPattern)value).Move(x,y); }
    public void Resize(double w,double h) { ((IUIAutomationTransformPattern)value).Resize(w,h); }
}
