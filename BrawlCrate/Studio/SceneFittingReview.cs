using System;

namespace BrawlCrate.Studio
{
    public sealed class SceneFittingOptions
    {
        public bool AutomaticFitting {get;set;}=true;public bool AllowOverlap {get;set;}=true;
        public int RootPolicy {get;set;}public int Scene {get;set;}=1;public int SurfaceMode {get;set;}
        public float? SourceFloor {get;set;}public float? TargetFloor {get;set;}
        public float SourceEdge {get;set;}public float TargetEdge {get;set;}public bool ReverseEdge {get;set;}
        public float? LimbTolerancePercent {get;set;}public double TrimStartSeconds {get;set;}public double? TrimEndSeconds {get;set;}
        public double ExportSpeed {get;set;}=1;public bool LossesReviewed {get;set;}
    }
    public sealed class SceneFittingReviewState
    {
        public string Key {get;set;}public string Options {get;set;}public string Receipt {get;set;}
        public LibraryClipReference Source {get;set;}public string TargetFingerprint {get;set;}
        public string ContextPath {get;set;}public string ContextClip {get;set;}public string ContextHash {get;set;}
    }
    internal sealed class SceneFittingReply
    {
        public FittingContextClip[] ContextClips {get;set;}
        public string Summary {get;set;}
        public string State {get;set;}public string Dependency {get;set;}public string Algorithm {get;set;}public bool ApplyAllowed {get;set;}public bool MeasuredReady {get;set;}public string Receipt {get;set;}
        public SceneFittingIssue[] Diagnostics {get;set;}public string[] Losses {get;set;}public string[] MissingSourceBones {get;set;}
        public string SourceName {get;set;}public string SourceClip {get;set;}public int SourceFrames {get;set;}public int TargetFrames {get;set;}public string Context {get;set;}
        public float[] SourceUp {get;set;}public float[] TargetUp {get;set;}public float[] SourceForward {get;set;}public float[] TargetForward {get;set;}
        public bool SourceNeedsReview {get;set;}public bool TargetNeedsReview {get;set;}public ControlTrack[] PreviewTracks {get;set;}public bool PreviewLoop {get;set;}public bool PreviewOnly {get;set;}
    }
    internal sealed class FittingContextClip {public string Id {get;set;}public string Name {get;set;}public int SampleCount {get;set;}public override string ToString()=>Name+" · "+SampleCount+" samples";}
    internal sealed class SceneFittingIssue {public string Code {get;set;}public string Message {get;set;}public bool Error {get;set;}}
}
