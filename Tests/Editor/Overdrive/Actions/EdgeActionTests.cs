using System;
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
using UnityEditor.GraphToolsFoundation.Overdrive.BasicModel;
using UnityEngine;

// ReSharper disable AccessToStaticMemberViaDerivedType

namespace UnityEditor.GraphToolsFoundation.Overdrive.Tests.Actions
{
    [Category("Edge")]
    [Category("Action")]
    public class EdgeActionTests : BaseFixture
    {
        protected override bool CreateGraphOnStartup => true;
        protected override Type CreatedGraphType => typeof(ClassStencil);

        [Test]
        public void Test_CreateEdgeAction_OneEdge([Values] TestingMode mode)
        {
            var node0 = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", new Vector2(-200, 0));
            var node1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", new Vector2(200, 0));

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));
                    Assert.That(node0.Input0, Is.Not.ConnectedTo(node1.Output0));
                    return new CreateEdgeAction(node0.Input0, node1.Output0);
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(node0.Input0, Is.ConnectedTo(node1.Output0));
                });
        }

        // no undo as it doesn't do anything
        [Test]
        public void Test_CreateEdgeAction_Duplicate([Values(TestingMode.Action)] TestingMode mode)
        {
            var node0 = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", new Vector2(-200, 0));
            var node1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", new Vector2(200, 0));

            GraphModel.CreateEdge(node0.Input0, node1.Output0);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(node0.Input0, Is.ConnectedTo(node1.Output0));
                    return new CreateEdgeAction(node0.Input0, node1.Output0);
                },
                () =>
                {
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(node0.Input0, Is.ConnectedTo(node1.Output0));
                });
        }

        public enum ItemizeTestType
        {
            Enabled, Disabled
        }

        static IEnumerable<object[]> GetItemizeTestCases()
        {
            foreach (TestingMode testingMode in Enum.GetValues(typeof(TestingMode)))
            {
                // test both itemize option and non ItemizeTestType option
                foreach (ItemizeTestType itemizeTest in Enum.GetValues(typeof(ItemizeTestType)))
                {
                    yield return MakeItemizeTestCase(testingMode, itemizeTest,
                        graphModel =>
                        {
                            string name = "myInt";
                            var decl = graphModel.CreateGraphVariableDeclaration(name, typeof(int).GenerateTypeHandle(), ModifierFlags.None, true);
                            return graphModel.CreateVariableNode(decl, Vector2.zero);
                        }
                    );

                    yield return MakeItemizeTestCase(testingMode, itemizeTest,
                        graphModel =>
                        {
                            string name = "myInt";
                            return graphModel.CreateConstantNode(name, typeof(int).GenerateTypeHandle(), Vector2.zero);
                        });
                }
            }
        }

        static object[] MakeItemizeTestCase(TestingMode testingMode, ItemizeTestType itemizeTest, Func<GraphModel, IInOutPortsNode> makeNode)
        {
            return new object[] { testingMode, itemizeTest, makeNode };
        }

        [Test, TestCaseSource(nameof(GetItemizeTestCases))]
        public void Test_CreateEdgeAction_Itemize(TestingMode testingMode, ItemizeTestType itemizeTest, Func<GraphModel, IInOutPortsNode> makeNode)
        {
            // create int node
            IInOutPortsNode node0 = makeNode(GraphModel);

            var opNode = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", Vector2.zero);

            // connect int to first input
            m_Store.Dispatch(new CreateEdgeAction(opNode.Input0, node0.OutputsByDisplayOrder.First()));
            m_Store.Update();

            // test how the node reacts to getting connected a second time
            TestPrereqActionPostreq(testingMode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref opNode);
                    var binOp = GraphModel.NodeModels.OfType<Type0FakeNodeModel>().First();
                    IPortModel input0 = binOp.Input0;
                    IPortModel input1 = binOp.Input1;
                    IPortModel binOutput = binOp.Output0;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(input0, Is.ConnectedTo(node0.OutputsByDisplayOrder.First()));
                    Assert.That(input1, Is.Not.ConnectedTo(node0.OutputsByDisplayOrder.First()));
                    Assert.That(binOutput.IsConnected, Is.False);
                    return new CreateEdgeAction(input1, node0.OutputsByDisplayOrder.First(),
                        createItemizedNode: itemizeTest == ItemizeTestType.Enabled);
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref opNode);
                    var binOp = GraphModel.NodeModels.OfType<Type0FakeNodeModel>().First();
                    IPortModel input0 = binOp.Input0;
                    IPortModel input1 = binOp.Input1;
                    IPortModel binOutput = binOp.Output0;
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(input0, Is.ConnectedTo(node0.OutputsByDisplayOrder.First()));
                    Assert.That(binOutput.IsConnected, Is.False);

                    if (itemizeTest == ItemizeTestType.Enabled)
                    {
                        Assert.That(GetNodeCount(), Is.EqualTo(3));
                        ISingleOutputPortNode newNode = GetNode(2) as ISingleOutputPortNode;
                        Assert.NotNull(newNode);
                        Assert.That(newNode, Is.TypeOf(node0.GetType()));
                        IPortModel output1 = newNode.OutputPort;
                        Assert.That(input1, Is.ConnectedTo(output1));
                    }
                    else
                    {
                        Assert.That(GetNodeCount(), Is.EqualTo(2));
                    }
                });
        }

        [Test]
        public void Test_CreateEdgeAction_ManyEdge([Values] TestingMode mode)
        {
            var node0 = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", new Vector2(-200, 0));
            var node1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", new Vector2(200, 0));

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    var input0 = node0.Input0;
                    var input1 = node0.Input1;
                    var input2 = node0.Input2;
                    var output0 = node1.Output0;
                    var output1 = node1.Output1;
                    var output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));
                    Assert.That(input0, Is.Not.ConnectedTo(output0));
                    Assert.That(input1, Is.Not.ConnectedTo(output1));
                    Assert.That(input2, Is.Not.ConnectedTo(output2));
                    return new CreateEdgeAction(input0, output0);
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    var input0 = node0.Input0;
                    var input1 = node0.Input1;
                    var input2 = node0.Input2;
                    var output0 = node1.Output0;
                    var output1 = node1.Output1;
                    var output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(input0, Is.ConnectedTo(output0));
                    Assert.That(input1, Is.Not.ConnectedTo(output1));
                    Assert.That(input2, Is.Not.ConnectedTo(output2));
                });

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    var input0 = node0.Input0;
                    var input1 = node0.Input1;
                    var input2 = node0.Input2;
                    var output0 = node1.Output0;
                    var output1 = node1.Output1;
                    var output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(input0, Is.ConnectedTo(output0));
                    Assert.That(input1, Is.Not.ConnectedTo(output1));
                    Assert.That(input2, Is.Not.ConnectedTo(output2));
                    return new CreateEdgeAction(input1, output1);
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    var input0 = node0.Input0;
                    var input1 = node0.Input1;
                    var input2 = node0.Input2;
                    var output0 = node1.Output0;
                    var output1 = node1.Output1;
                    var output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(input0, Is.ConnectedTo(output0));
                    Assert.That(input1, Is.ConnectedTo(output1));
                    Assert.That(input2, Is.Not.ConnectedTo(output2));
                });

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    var input0 = node0.Input0;
                    var input1 = node0.Input1;
                    var input2 = node0.Input2;
                    var output0 = node1.Output0;
                    var output1 = node1.Output1;
                    var output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(input0, Is.ConnectedTo(output0));
                    Assert.That(input1, Is.ConnectedTo(output1));
                    Assert.That(input2, Is.Not.ConnectedTo(output2));
                    return new CreateEdgeAction(input2, output2);
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    var input0 = node0.Input0;
                    var input1 = node0.Input1;
                    var input2 = node0.Input2;
                    var output0 = node1.Output0;
                    var output1 = node1.Output1;
                    var output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(3));
                    Assert.That(input0, Is.ConnectedTo(output0));
                    Assert.That(input1, Is.ConnectedTo(output1));
                    Assert.That(input2, Is.ConnectedTo(output2));
                });
        }

        static IEnumerable<object[]> GetCreateTestCases()
        {
            foreach (TestingMode testingMode in Enum.GetValues(typeof(TestingMode)))
            {
                yield return new object[] { testingMode };
            }
        }

        [Test]
        public void Test_CreateNodeFromOutputPort_NoNodeCreated([Values] TestingMode mode)
        {
            var node1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", Vector2.zero);
            var node2 = GraphModel.CreateNode<Type0FakeNodeModel>("Node2", Vector2.zero);
            var edge = GraphModel.CreateEdge(node2.Input0, node1.Output0);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    return new CreateNodeFromPortAction(
                        node1.Output0,
                        Vector2.down,
                        new GraphNodeModelSearcherItem(new NodeSearcherItemData(typeof(int)), data => null, ""),
                        new List<IEdgeModel> { edge });
                },
                () =>
                {
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));
                });
        }

        [Test, TestCaseSource(nameof(GetCreateTestCases))]
        public void Test_CreateNodeFromOutputPort_NoConnection(TestingMode testingMode)
        {
            var gedb = new GraphElementSearcherDatabase(Stencil, GraphModel);
            Type0FakeNodeModel.AddToSearcherDatabase(gedb);
            var db = gedb.Build();
            var item = (GraphNodeModelSearcherItem)db.Search(nameof(Type0FakeNodeModel), out _)[0];

            var node0 = GraphModel.CreateNode<Type1FakeNodeModel>("Node0", Vector2.zero);
            var output0 = node0.Output;

            TestPrereqActionPostreq(testingMode,
                () =>
                {
                    Assert.That(GetNodeCount(), Is.EqualTo(1));
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));
                    return new CreateNodeFromPortAction(output0, Vector2.down, item);
                },
                () =>
                {
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));

                    var newNode = GetNode(1);
                    Assert.That(newNode, Is.TypeOf<Type0FakeNodeModel>());

                    var portModel = node0.OutputsByDisplayOrder.First();
                    Assert.That(portModel?.GetConnectedPorts().Count(), Is.EqualTo(0));
                });
        }

        [Test, TestCaseSource(nameof(GetCreateTestCases))]
        public void Test_CreateNodeFromOutputPort(TestingMode testingMode)
        {
            var gedb = new GraphElementSearcherDatabase(Stencil, GraphModel);
            Type3FakeNodeModel.AddToSearcherDatabase(gedb);
            var db = gedb.Build();
            var item = (GraphNodeModelSearcherItem)db.Search(nameof(Type3FakeNodeModel), out _)[0];

            var node0 = GraphModel.CreateNode<Type3FakeNodeModel>("Node0", Vector2.zero);

            TestPrereqActionPostreq(testingMode,
                () =>
                {
                    RefreshReference(ref node0);
                    var output0 = node0.Output;
                    Assert.That(GetNodeCount(), Is.EqualTo(1));
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));
                    return new CreateNodeFromPortAction(output0, Vector2.down, item);
                },
                () =>
                {
                    RefreshReference(ref node0);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));

                    var newNode = GetNode(1);
                    Assert.That(newNode, Is.TypeOf<Type3FakeNodeModel>());

                    var newEdge = GetEdge(0);
                    Assert.That(newEdge.ToPort.DataTypeHandle, Is.EqualTo(newEdge.FromPort.DataTypeHandle));

                    var portModel = node0.Output;
                    Assert.That(portModel.GetConnectedPorts().Single(), Is.EqualTo(newNode.InputsByDisplayOrder.First()));
                });
        }

        [Test]
        public void Test_DeleteElementsAction_OneEdge([Values] TestingMode mode)
        {
            var node0 = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", new Vector2(-200, 0));
            var node1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", new Vector2(200, 0));
            var input0 = node0.Input0;
            var input1 = node0.Input1;
            var input2 = node0.Input2;
            var output0 = node1.Output0;
            var output1 = node1.Output1;
            var output2 = node1.Output2;
            var edge0 = GraphModel.CreateEdge(input0, output0);
            GraphModel.CreateEdge(input1, output1);
            GraphModel.CreateEdge(input2, output2);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    edge0 = GetEdge(0);
                    input0 = node0.Input0;
                    input1 = node0.Input1;
                    input2 = node0.Input2;
                    output0 = node1.Output0;
                    output1 = node1.Output1;
                    output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(3));
                    Assert.That(input0, Is.ConnectedTo(output0));
                    Assert.That(input1, Is.ConnectedTo(output1));
                    Assert.That(input2, Is.ConnectedTo(output2));
                    return new DeleteElementsAction(new[] { edge0 });
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    RefreshReference(ref edge0);
                    input0 = node0.Input0;
                    input1 = node0.Input1;
                    input2 = node0.Input2;
                    output0 = node1.Output0;
                    output1 = node1.Output1;
                    output2 = node1.Output2;
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(input0, Is.Not.ConnectedTo(output0));
                    Assert.That(input1, Is.ConnectedTo(output1));
                    Assert.That(input2, Is.ConnectedTo(output2));
                });
        }

        [Test]
        public void Test_DeleteElementsAction_ManyEdges([Values] TestingMode mode)
        {
            var node0 = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", new Vector2(-200, 0));
            var node1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", new Vector2(200, 0));
            GraphModel.CreateEdge(node0.Input0, node1.Output0);
            GraphModel.CreateEdge(node0.Input1, node1.Output1);
            GraphModel.CreateEdge(node0.Input2, node1.Output2);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(3));
                    Assert.That(node0.Input0, Is.ConnectedTo(node1.Output0));
                    Assert.That(node0.Input1, Is.ConnectedTo(node1.Output1));
                    Assert.That(node0.Input2, Is.ConnectedTo(node1.Output2));
                    var edge0 = GraphModel.EdgeModels.First(e => PortModel.Equivalent(e.ToPort, node0.Input0));
                    Assert.IsTrue(edge0.IsDeletable());
                    return new DeleteElementsAction(new[] { edge0 });
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(node0.Input0, Is.Not.ConnectedTo(node1.Output0));
                    Assert.That(node0.Input1, Is.ConnectedTo(node1.Output1));
                    Assert.That(node0.Input2, Is.ConnectedTo(node1.Output2));
                });

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(node0.Input0, Is.Not.ConnectedTo(node1.Output0));
                    Assert.That(node0.Input1, Is.ConnectedTo(node1.Output1));
                    Assert.That(node0.Input2, Is.ConnectedTo(node1.Output2));
                    var edge1 = GraphModel.EdgeModels.First(e => PortModel.Equivalent(e.ToPort, node0.Input1));
                    Assert.IsTrue(edge1.IsDeletable());
                    return new DeleteElementsAction(new[] { edge1 });
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(node0.Input0, Is.Not.ConnectedTo(node1.Output0));
                    Assert.That(node0.Input1, Is.Not.ConnectedTo(node1.Output1));
                    Assert.That(node0.Input2, Is.ConnectedTo(node1.Output2));
                });

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(node0.Input0, Is.Not.ConnectedTo(node1.Output0));
                    Assert.That(node0.Input1, Is.Not.ConnectedTo(node1.Output1));
                    Assert.That(node0.Input2, Is.ConnectedTo(node1.Output2));
                    var edge2 = GraphModel.EdgeModels.First(e => PortModel.Equivalent(e.ToPort, node0.Input2));
                    Assert.IsTrue(edge2.IsDeletable());
                    return new DeleteElementsAction(new[] { edge2 });
                },
                () =>
                {
                    RefreshReference(ref node0);
                    RefreshReference(ref node1);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(0));
                    Assert.That(node0.Input0, Is.Not.ConnectedTo(node1.Output0));
                    Assert.That(node0.Input1, Is.Not.ConnectedTo(node1.Output1));
                    Assert.That(node0.Input2, Is.Not.ConnectedTo(node1.Output2));
                });
        }

        [Test]
        public void Test_SplitEdgeAndInsertNodeAction([Values] TestingMode mode)
        {
            var constant = GraphModel.CreateConstantNode("Constant", typeof(int).GenerateTypeHandle(), Vector2.zero);
            var binary0 = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", Vector2.zero);
            var binary1 = GraphModel.CreateNode<Type0FakeNodeModel>("Node1", Vector2.zero);
            GraphModel.CreateEdge(binary0.Input0, constant.OutputPort);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref constant);
                    RefreshReference(ref binary0);
                    RefreshReference(ref binary1);
                    var edge = GetEdge(0);
                    Assert.That(GetNodeCount(), Is.EqualTo(3));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(binary0.Input0, Is.ConnectedTo(constant.OutputPort));
                    return new SplitEdgeAndInsertExistingNodeAction(edge, binary1);
                },
                () =>
                {
                    RefreshReference(ref constant);
                    RefreshReference(ref binary0);
                    RefreshReference(ref binary1);
                    Assert.That(GetNodeCount(), Is.EqualTo(3));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(binary1.Input0, Is.ConnectedTo(constant.OutputPort));
                    Assert.That(binary0.Input0, Is.ConnectedTo(binary1.Output0));
                });
        }

        [Test]
        public void TestCreateNodeOnEdge_BothPortsConnected([Values] TestingMode mode)
        {
            var constant = GraphModel.CreateConstantNode("int", typeof(int).GenerateTypeHandle(), Vector2.zero);
            var unary = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", Vector2.zero);
            var edge = GraphModel.CreateEdge(unary.Input0, constant.OutputPort);

            var gedb = new GraphElementSearcherDatabase(Stencil, GraphModel);
            Type0FakeNodeModel.AddToSearcherDatabase(gedb);
            var db = gedb.Build();
            var item = (GraphNodeModelSearcherItem)db.Search(nameof(Type0FakeNodeModel), out _)[0];

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref unary);
                    RefreshReference(ref constant);
                    edge = GetEdge(0);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(1));
                    Assert.That(unary.Input0, Is.ConnectedTo(constant.OutputPort));
                    return new CreateNodeOnEdgeAction(edge, Vector2.zero, item);
                },
                () =>
                {
                    RefreshReference(ref unary);
                    RefreshReference(ref constant);
                    RefreshReference(ref edge);
                    var unary2 = GraphModel.NodeModels.OfType<Type0FakeNodeModel>().ToList()[1];

                    Assert.IsNotNull(unary2);
                    Assert.That(GetNodeCount(), Is.EqualTo(3));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));
                    Assert.That(constant.OutputPort, Is.ConnectedTo(unary2.Input0));
                    Assert.That(unary2.Output0, Is.ConnectedTo(unary.Input0));
                    Assert.IsFalse(GraphModel.EdgeModels.Contains(edge));
                }
            );
        }

        [Test]
        public void TestCreateNodeOnEdge_WithOutputNodeConnectedToUnknown([Values] TestingMode mode)
        {
            var constantNode = GraphModel.CreateConstantNode("int1", typeof(int).GenerateTypeHandle(), Vector2.zero);
            var addNode = GraphModel.CreateNode<Type0FakeNodeModel>("Node0", Vector2.zero);
            GraphModel.CreateEdge(addNode.Input0, constantNode.OutputPort);
            GraphModel.CreateEdge(addNode.Input1, constantNode.OutputPort);

            var gedb = new GraphElementSearcherDatabase(Stencil, GraphModel);
            Type0FakeNodeModel.AddToSearcherDatabase(gedb);
            var db = gedb.Build();
            var item = (GraphNodeModelSearcherItem)db.Search(nameof(Type0FakeNodeModel), out _)[0];

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref addNode);
                    RefreshReference(ref constantNode);
                    Assert.That(GetNodeCount(), Is.EqualTo(2));
                    Assert.That(GetEdgeCount(), Is.EqualTo(2));

                    Assert.That(addNode, Is.Not.Null);
                    Assert.That(addNode.Input0, Is.ConnectedTo(constantNode.OutputPort));
                    var edge = GraphModel.EdgeModels.First();
                    return new CreateNodeOnEdgeAction(edge, Vector2.zero, item);
                },
                () =>
                {
                    RefreshReference(ref addNode);
                    RefreshReference(ref constantNode);
                    var multiplyNode = GraphModel.NodeModels.OfType<Type0FakeNodeModel>().ToList()[1];

                    Assert.IsNotNull(multiplyNode);
                    Assert.That(GetNodeCount(), Is.EqualTo(3));
                    Assert.That(GetEdgeCount(), Is.EqualTo(3));
                    Assert.That(constantNode.OutputPort, Is.ConnectedTo(multiplyNode.Input0));
                    Assert.That(multiplyNode.Output0, Is.ConnectedTo(addNode.Input0));
                    Assert.That(constantNode.OutputPort, Is.Not.ConnectedTo(addNode.Input0));
                }
            );
        }

        [Test]
        public void TestEdgeReorderAction([Values] TestingMode mode, [Values] ReorderEdgeAction.ReorderType reorderType)
        {
            var originNode = GraphModel.CreateNode<Type0FakeNodeModel>("Origin", Vector2.zero);
            var destNode1 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest1", Vector2.zero);
            var destNode2 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest2", Vector2.zero);
            var destNode3 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest3", Vector2.zero);
            var destNode4 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest4", Vector2.zero);
            var destNode5 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest5", Vector2.zero);

            var edge1 = GraphModel.CreateEdge(destNode1.ExeInput0, originNode.ExeOutput0);
            var edge2 = GraphModel.CreateEdge(destNode2.ExeInput0, originNode.ExeOutput0);
            var edge3 = GraphModel.CreateEdge(destNode3.ExeInput0, originNode.ExeOutput0);
            var edge4 = GraphModel.CreateEdge(destNode4.ExeInput0, originNode.ExeOutput0);
            var edge5 = GraphModel.CreateEdge(destNode5.ExeInput0, originNode.ExeOutput0);

            TestPrereqActionPostreq(mode,
                () =>
                {
                    RefreshReference(ref originNode);
                    RefreshReference(ref edge1);
                    RefreshReference(ref edge2);
                    RefreshReference(ref edge3);
                    RefreshReference(ref edge4);
                    RefreshReference(ref edge5);

                    Assert.IsTrue(originNode.ExeOutput0.HasReorderableEdges);
                    Assert.AreEqual(2, GraphModel.EdgeModels.IndexOf(edge3));

                    return new ReorderEdgeAction(edge3, reorderType);
                },
                () =>
                {
                    RefreshReference(ref edge1);
                    RefreshReference(ref edge2);
                    RefreshReference(ref edge3);
                    RefreshReference(ref edge4);
                    RefreshReference(ref edge5);

                    int expectedIdx;
                    switch (reorderType)
                    {
                        case ReorderEdgeAction.ReorderType.MoveFirst:
                            expectedIdx = 0;
                            break;
                        case ReorderEdgeAction.ReorderType.MoveUp:
                            expectedIdx = 1;
                            break;
                        case ReorderEdgeAction.ReorderType.MoveDown:
                            expectedIdx = 3;
                            break;
                        case ReorderEdgeAction.ReorderType.MoveLast:
                            expectedIdx = 4;
                            break;
                        default:
                            throw new ArgumentOutOfRangeException(nameof(reorderType), reorderType, "Unexpected value");
                    }
                    Assert.AreEqual(expectedIdx, GraphModel.EdgeModels.IndexOf(edge3));
                }
            );
        }

        [Test]
        public void TestEdgeReorderActionWorksOnlyWithReorderableEdgePorts([Values] ReorderEdgeAction.ReorderType reorderType)
        {
            var originNode = GraphModel.CreateNode<Type0FakeNodeModel>("Origin", Vector2.zero);
            var destNode1 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest1", Vector2.zero);
            var destNode2 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest2", Vector2.zero);
            var destNode3 = GraphModel.CreateNode<Type0FakeNodeModel>("Dest3", Vector2.zero);

            GraphModel.CreateEdge(destNode1.Input0, originNode.Output0);
            var edge2 = GraphModel.CreateEdge(destNode2.Input0, originNode.Output0);
            GraphModel.CreateEdge(destNode3.Input0, originNode.Output0);

            const int immutableIdx = 1;

            Assert.IsFalse(originNode.Output0.HasReorderableEdges);
            Assert.AreEqual(immutableIdx, GraphModel.EdgeModels.IndexOf(edge2));

            m_Store.Dispatch(new ReorderEdgeAction(edge2, reorderType));

            // Nothing has changed.
            Assert.AreEqual(immutableIdx, GraphModel.EdgeModels.IndexOf(edge2));
        }
    }
}
