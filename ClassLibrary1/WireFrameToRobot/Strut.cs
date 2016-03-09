﻿using Autodesk.DesignScript.Geometry;
using Autodesk.DesignScript.Runtime;
using System;
using System.Collections.Generic;
using System.Linq;
using Autodesk.DesignScript.Interfaces;
namespace WireFrameToRobot
{
    public class Strut:ILabelAble,IDisposable,IGraphicItem
    {
        /// <summary>
        /// an ID generated based on the nodes this strut is connected to
        /// </summary>
        public string ID { get; private set; }
        /// <summary>
        /// the line which represents this strut
        /// </summary>
        public Line LineRepresentation { get; private set; }
        /// <summary>
        /// the node which this strut object belongs to
        /// </summary>
        public Node OwnerNode { get; private set; }
        /// <summary>
        /// the diameter of the strut geometry
        /// </summary>
        public double Diameter { get; private set; }
        /// <summary>
        /// a solid circular sweep along the line
        /// </summary>
        public Solid StrutGeometry { get; private set; }
       
        /// <summary>
        /// get labels for the strut - showing its ID as geometry
        /// </summary>
        /// <param name="scale"></param>
        /// <returns></returns>
        public List<Curve> GetLabels(double scale =30)
        {
            var label = new Label<Strut>(this, scale);
            var output = label.AlignedLabelGeometry;
            label.Dispose();
            return output;
        }

        /// <summary>
        /// this plane represents the cut vector
        /// </summary>
        private Plane CutPlane { get
            {
                var coordinateSystemOnLine = LineRepresentation.CoordinateSystemAtParameter(0);
                //reverse the normal because we want the plane normal to point towards the node
                //TODO(mike + Nick) we need to verify this is correct

                var org = coordinateSystemOnLine.Origin;
                var yaxis = coordinateSystemOnLine.YAxis;
                var zaxis = coordinateSystemOnLine.ZAxis;
                var yrev = yaxis.Reverse();

                var output = Plane.ByOriginNormalXAxis(org, yrev, zaxis);
                org.Dispose();
                yaxis.Dispose();
                zaxis.Dispose();
                yrev.Dispose();
                coordinateSystemOnLine.Dispose();

                return output;
                    }
        }
       
        /// <summary>
        /// get the cut plane after it has been transformed around the origin (using the inverse transform of its owner node)
        /// </summary>
        public Plane TransformedCutPlane
        {
            get
            {
                var cs = OwnerNode.OrientedNodeGeometry.ContextCoordinateSystem;
                var inverse = cs.Inverse();
                var output = CutPlane.Transform(inverse) as Plane;
                cs.Dispose();
                inverse.Dispose();
                return output;
            }
        }
        /// <summary>
        /// This returns the transformedCutPlane after its been rotated around the Z axis so that its X axis
        /// aligns with guide vector - NOTE, this does not seem to work in all cases but apparently solves the
        /// robot reach issues we were having
        /// </summary>
        /// <param name="alignTo"></param>
        /// <returns></returns>
        public Plane TransformedAndAlignedCutPlane([DefaultArgumentAttribute("Vector.ByCoordinates(1,0,0)")]Vector alignTo)
        {
            var p = this.TransformedCutPlane;
            var dot = p.XAxis.Dot(alignTo);
            double angle = Math.Acos(dot) * (-1) * (180 / Math.PI);
            var output = p.Rotate(p.Origin, p.Normal, angle) as Plane;
            p.Dispose();
            return output;
        }

        /// <summary>
        /// This method returns a coordinateSystem from the TransformedAlignedCutPlane, this is useful for visualization
        /// The coordinateSystems will appear at the origin since they are transformed using the inverse
        /// </summary>
        /// <returns></returns>
        public CoordinateSystem AlignedCoordinateSystem([DefaultArgumentAttribute("Vector.ByCoordinates(1,0,0)")]Vector alignTo)
        {
            var plane = this.TransformedAndAlignedCutPlaneUsingMarching(alignTo);
            var output = CoordinateSystem.ByPlane(plane);
            plane.Dispose();
            return output;
        }
        
        /// <summary>
        /// attempts to find an aligned plane such that the X axis of the cut plane matches the guide vector using rotation marching, gets a nearly ~aligned~ plane
        /// </summary>
        /// <param name="alignTo"></param>
        /// <returns></returns>
        public Plane TransformedAndAlignedCutPlaneUsingMarching([DefaultArgumentAttribute("Vector.ByCoordinates(1,0,0)")]Vector alignTo)
        {
            var p = this.TransformedCutPlane;
            var random = new Random();
            var max = 5.0;
            var min = .0001;
            var angle = random.NextDouble() * (max - min) + min;

            var parentFit = Math.Abs(p.XAxis.Y);
            //while the y component is not zero or the x alignment is facing opposite directions
            while (parentFit > .01 || alignTo.Dot(p.XAxis) < 0  )
            {
                angle = random.NextDouble() * (max - min) + min;
                var child = p.Rotate(p.Origin, p.Normal, angle) as Plane;
                //make sure to dispose the old plane
                p.Dispose();
                var childFit = Math.Abs(child.XAxis.Y);
                p = child;
                //recalculate parentFit
                parentFit = Math.Abs(p.XAxis.Y);
            }
         return p;
        }
        
        public Solid GeometryToLabel
        {
            get
            {
                return StrutGeometry;
            }
        }

        /// <summary>
        /// construct a new strut object, this method computes the strut geometry and caches it on the strut
        /// </summary>
        /// <param name="line"></param>
        /// <param name="diameter"></param>
        /// <param name="owner"></param>
        public Strut(Line line, double diameter, Node owner)
        {
            LineRepresentation = line;
            OwnerNode = owner;
            Diameter = diameter; //mm

            //construct a swept tube along the strut line
            var startPlane = LineRepresentation.PlaneAtParameter(0);
            var circle = Circle.ByPlaneRadius(startPlane, Diameter / 2);
            var swept = circle.SweepAsSolid(LineRepresentation);
            circle.Dispose();
            startPlane.Dispose();
            StrutGeometry = swept;
        }
        internal void SetId(string id)
        {
            ID = id;
        }

        /// <summary>
        /// check if the cutplane normal angle is withing 30 degrees of the holder face of the node
        /// </summary>
        /// <returns></returns>
        public bool StrutInHolderExclusionZone()
        {
            var face = OwnerNode.HolderFace;
            var anglebetweenWorldZandCutPlaneZ = face.NormalAtParameter(.5,.5).AngleBetween(CutPlane.Normal);
            face.Dispose();
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
            StrutGeometry.Dispose();
        }

        /// <summary>
        /// a hashcode based on the string of the start and end point
        /// </summary>
        /// <returns></returns>
        public int SpatialHash()
        {
            unchecked
            {
                var hash = 0;
                hash = hash ^ LineRepresentation.StartPoint.ToString().GetHashCode();
                hash = hash ^ LineRepresentation.EndPoint.ToString().GetHashCode();

                return hash;
            }
        }

        public void Tessellate(IRenderPackage package, TessellationParameters parameters)
        {
            StrutGeometry.Tessellate(package, parameters);
        }
    }
}
namespace WireFrameToRobot.Extensions
{
    public static class GeometryExtensions
    {
        /// <summary>
        /// prune duplicate lines by using start and endpoints to check for equality
        /// </summary>
        /// <param name="allLines"></param>
        /// <returns></returns>
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

        /// <summary>
        /// checks equality between lines by checking if the start and endpoints match,
        /// even if these are reversed this method return true.
        /// </summary>
        /// <param name="a"></param>
        /// <param name="b"></param>
        /// <returns></returns>
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
