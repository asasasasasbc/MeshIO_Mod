﻿using MeshIO.Entities;
using MeshIO.Shaders;
using System;
using System.Collections.Generic;

namespace MeshIO
{
	public class Node : SceneElement
	{

        

        public void AddChildNode(Node child)
        {
            if (child != null)
            {
                this.Nodes.Add(child);
                child.Parent = this; // Set parent
            }
        }

        public void RemoveChildNode(Node child)
        {
            if (child != null && this.Nodes.Remove(child))
            {
                child.Parent = null; // Clear parent
            }
        }

        /// <summary>
        /// The node and all the components are visible or not
        /// </summary>
        public bool IsVisible { get; set; } = true;

		/// <summary>
		/// Get the local transform for this node
		/// </summary>
		public Transform Transform { get; internal set; } = new Transform();

        /// <summary>
        /// Get the parent for this node
        /// </summary>
        public Element3D Parent { get; internal set; } // Make settable within MeshIO assembly

        public List<Node> Nodes { get; } = new();

		public List<Material> Materials { get; } = new();

		public List<Entity> Entities { get; } = new();

		public Node() : base() { }

		public Node(string name) : base(name) { }

		/// <summary>
		/// Get the global transformation for this node
		/// </summary>
		/// <returns></returns>
		/// <exception cref="NotImplementedException"></exception>
		public Transform GetGlobalTransform()
		{
			throw new NotImplementedException();
		}
	}
}
