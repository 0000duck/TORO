﻿using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
namespace WireFrameToRobot
{
    public class Strut:ILabelAble,IDisposable
    {
        public string ID { get; private set; }
        public Line LineRepresentation { get; private set; }
        public Node OwnerNode { get; private set; }
        public double Diameter { get; private set; }
        public Solid StrutGeometry
        {
            get
            {
                //construct a swept tube along the strut line
                var startPlane = LineRepresentation.PlaneAtParameter(0);
                var circle = Circle.ByPlaneRadius(startPlane, Diameter / 2);
                var swept = circle.SweepAsSolid(LineRepresentation);
                circle.Dispose();
                startPlane.Dispose();
                return swept;
            }
        }

        public List<Curve> GetLabels(double scale =30)
        {
            var label = new Label<Strut>(this, scale);
            var output = label.AlignedLabelGeometry;
            label.Dispose();
            return output;
        }

        private Plane CutPlane { get
            {
                var coordinateSystemOnLine = LineRepresentation.CoordinateSystemAtParameter(0);
                //reverse the normal because we want the plane normal to point towards the node
                //TODO(mike + Nick) we need to verify this is correct
                return Plane.ByOriginNormalXAxis(coordinateSystemOnLine.Origin,coordinateSystemOnLine.YAxis.Reverse(), coordinateSystemOnLine.ZAxis); }
        }
       

        public Plane TransformedCutPlane
        {
            get
            {
                var inverse = OwnerNode.OrientedNodeGeometry.ContextCoordinateSystem.Inverse();
                return CutPlane.Transform(inverse) as Plane;
            }
        }

        public Solid GeometryToLabel
        {
            get
            {
                return StrutGeometry;
            }
        }

        public Strut(Line line, double diameter, Node owner)
        {
            LineRepresentation = line;
            OwnerNode = owner;
            Diameter = diameter; //mm
        }
        internal void SetId(string id)
        {
            ID = id;
        }

        public bool StrutInHolderExclusionZone()
        {
            var anglebetweenWorldZandCutPlaneZ = OwnerNode.HolderFace.NormalAtParameter(.5,.5).AngleBetween(CutPlane.Normal);
            if(anglebetweenWorldZandCutPlaneZ > 30)
            {
                return false;
            }
            return true;
        }

        public void Dispose()
        {
           if(LineRepresentation.Tags.LookupTag("dispose") != null)
            {
                LineRepresentation.Dispose();
            }
        }
    }
}
namespace WireFrameToRobot.Extensions
{
    public static class GeometryExtensions
    {
        public static IEnumerable<Line> PruneDuplicates (List<Line> allLines)
            {
            var output = new List<Line>();

            foreach(var line in allLines)
            {
                if (output.Any(x => line.SameLine(x)))
                {
                    continue;
                }
                else
                {
                    output.Add(line);
                }
            }
            return output;

            }

        public static bool SameLine (this Line a, Line b)
            {
            var astart = a.StartPoint;
            var aend = a.EndPoint;
            var bstart = b.StartPoint;
            var bend = b.EndPoint;

            var oldgeo = new List<Geometry>(){ astart,aend,bstart,bend};
            

            if ((astart.IsAlmostEqualTo(bstart) && aend.IsAlmostEqualTo(bend))
                || (aend.IsAlmostEqualTo(bstart) && (astart.IsAlmostEqualTo(bend))))
            {
                oldgeo.ForEach(x => x.Dispose());
                return true;
            }
            oldgeo.ForEach(x => x.Dispose());
            return false;
            }

    }
}
