using System;
using UnityEngine;
public struct CellOffset { public int x,y; public CellOffset(int x,int y) { this.x=x;this.y=y; } }
public enum ConduitType { None, Gas, Liquid, Solid }
public class ConduitPortInfo { public ConduitType conduitType; public CellOffset offset; }
public interface ISecondaryInput { bool HasSecondaryConduitType(ConduitType type); CellOffset GetSecondaryConduitOffset(ConduitType type); }
public interface ISecondaryOutput { bool HasSecondaryConduitType(ConduitType type); CellOffset GetSecondaryConduitOffset(ConduitType type); }
public abstract class SecondaryInputBoundary : Component,ISecondaryInput {
 public ConduitPortInfo portInfo; public int HasCalls,OffsetCalls;
 public bool HasSecondaryConduitType(ConduitType type) { HasCalls++;return portInfo.conduitType==type; }
 public CellOffset GetSecondaryConduitOffset(ConduitType type) { OffsetCalls++;return portInfo.offset; }
}
public abstract class SecondaryOutputBoundary : Component,ISecondaryOutput {
 public ConduitPortInfo portInfo; public int HasCalls,OffsetCalls;
 public bool HasSecondaryConduitType(ConduitType type) { HasCalls++;return portInfo.conduitType==type; }
 public CellOffset GetSecondaryConduitOffset(ConduitType type) { OffsetCalls++;return portInfo.offset; }
}
public class ConduitSecondaryInput : SecondaryInputBoundary { }
public class ConduitPreferentialFlow : SecondaryInputBoundary { }
public class JetSuitLocker : SecondaryInputBoundary { }
public class ConduitSecondaryOutput : SecondaryOutputBoundary { }
public class ConduitOverflow : SecondaryOutputBoundary { }
public class ElementFilter : SecondaryOutputBoundary { }
public abstract class MultiInputBoundary : Component,ISecondaryInput {
 public ConduitPortInfo gasPortInfo,liquidPortInfo,solidPortInfo;public int HasCalls,OffsetCalls;
 public bool HasSecondaryConduitType(ConduitType type){ HasCalls++;return gasPortInfo.conduitType==type||liquidPortInfo.conduitType==type||solidPortInfo.conduitType==type; }
 public CellOffset GetSecondaryConduitOffset(ConduitType type){ OffsetCalls++;return type==ConduitType.Gas?gasPortInfo.offset:type==ConduitType.Liquid?liquidPortInfo.offset:solidPortInfo.offset; }
}
public class RailGun : MultiInputBoundary { }
public class WarpConduitSender : MultiInputBoundary { }
public class WarpConduitReceiver : Component,ISecondaryOutput {
 public ConduitPortInfo gasPortInfo,liquidPortInfo,solidPortInfo;public int HasCalls,OffsetCalls;
 public bool HasSecondaryConduitType(ConduitType type){ HasCalls++;return gasPortInfo.conduitType==type||liquidPortInfo.conduitType==type||solidPortInfo.conduitType==type; }
 public CellOffset GetSecondaryConduitOffset(ConduitType type){ OffsetCalls++;return type==ConduitType.Gas?gasPortInfo.offset:type==ConduitType.Liquid?liquidPortInfo.offset:solidPortInfo.offset; }
}
