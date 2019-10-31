const renderGraph = () => {
  // Create a new directed graph
  const g = new dagreD3.graphlib.Graph().setGraph({})

  // Add the nodes
  for (const pkg of Object.keys(data)) {
    const name = data[pkg].name
    const ver = data[pkg].version
    const opts = { label: `${name}\n${ver}`, rx: 5, ry: 5 }
    if (data[pkg].internal) {
      opts.labelStyle = "font-weight: bold"
      opts.style = "fill: #7f7"
    }
    g.setNode(pkg, opts)
  }

  // Add the edges
  for (const pkg of Object.keys(data)) {
    if (data[pkg].collapsed) { continue }

    const deps = data[pkg].deps
    for (const dep of Object.keys(deps)) {
      const destName = `${dep}:${deps[dep]}`
      g.setEdge(pkg, destName, { label: '' })
    }
  }


  let stable
  do {
    stable = true
    for (const src of g.sources()) {
      console.log(src)
      if (!data[src].internal) {
        console.log(`removing ${src}`)
        g.removeNode(src)
        stable = false
      }
    }
  } while (!stable)

  for (const node of g.nodes()) {
    const nodeObj = g.node(node)
    if (data[node].collapsed && Object.keys(data[node].deps).length > 0) {
      nodeObj.label += '\n+'
    } else if (g.outEdges(node).length > 0) {
      nodeObj.label += '\n-'
    }
  }

  // for (const node of g.nodes()) {
  //   if (data[node].type !== "internal" && g.inEdges(node).length === 0) {
  //     console.log("orphan " + node)
  //     g.removeNode(node)
  //   }
  // }
  
  // Run the renderer. This is what draws the final graph.
  render(inner, g)

  d3.selectAll('svg g.node').on('click', (e) => {
    data[e].collapsed = !(data[e].collapsed)
    renderGraph()
  })

  // Center the graph
  const initialScale = 1.0
  svg.call(zoom.transform, d3.zoomIdentity.translate((svg.attr("width") - g.graph().width * initialScale) / 2, 20).scale(initialScale))
}

// Set up the render target
var svg = d3.select("svg"),
    inner = svg.append("g")

// Set up zoom support
var zoom = d3.zoom().on("zoom", function() {
  inner.attr("transform", d3.event.transform)
});
svg.call(zoom)

svg.attr('width', Math.max(
  document.body.scrollWidth,
  document.documentElement.scrollWidth,
  document.body.offsetWidth,
  document.documentElement.offsetWidth,
  document.documentElement.clientWidth
) - 2)

svg.attr('height', Math.max(
  document.body.scrollHeight,
  document.documentElement.scrollHeight,
  document.body.offsetHeight,
  document.documentElement.offsetHeight,
  document.documentElement.clientHeight
) - svg.node().getBoundingClientRect().y - 6)

var render = new dagreD3.render()
renderGraph()
