const renderGraph = () => {
  const config = {
    container: document.getElementById('cy'),
    elements: [],
    autoungrabify: true,
    autounselectify: true,

    layout: {
      name: 'dagre'
    },
  
    style: [
      {
        selector: 'node',
        style: {
          'width': 'label',
          'height': 'label',
          'padding': '8px',
          'label': 'data(label)',
          'shape': 'round-rectangle',
          'background-color': '#fff',
          'border-width': '1px',
          'border-color': '#333',
          'text-wrap': 'wrap',
          "text-valign": "center",
          "text-halign": "center"
        }
      },
      {
        selector: '.internal',
        style: {
          'background-color': '#7f7'
        }
      },
      {
        selector: '.collapsed',
        style: {
          'background-color': '#77f'
        }
      },
      {
        selector: '.hidden',
        style: {
          'display': 'none'
        }
      },
      {
        selector: 'edge',
        style: {
          'width': '1.5px',
          'target-arrow-shape': 'triangle',
          'line-color': '#333',
          'target-arrow-color': '#333',
          'curve-style': 'bezier'
        }
      }
    ]
  }

  // Add the nodes
  for (const pkg of Object.keys(data)) {
    const name = data[pkg].name
    const ver = data[pkg].version
    const ele = {
      data: {
        id: pkg,
        label: `${name}\n${ver}`
      }
    }

    if (data[pkg].internal) {
      ele.classes = 'internal'
    }
    config.elements.push(ele)
  }

  // Add the edges
  for (const pkg of Object.keys(data)) {
    const deps = data[pkg].deps
    for (const dep of Object.keys(deps)) {
      const dest = `${dep}:${deps[dep]}`
      const edge = {
        data: {
          id: `${pkg}:${dest}`,
          source: pkg,
          target: dest
        }
      }
      config.elements.push(edge)
    }
  }

  const cy = cytoscape(config)
  cy.on('tap', 'node', e => {
    if (e.target.outgoers().length === 0) { return }
    if (e.target.hasClass('collapsed')) {
      toggleCollapse(e.target, false, cy)
    } else {
      toggleCollapse(e.target, true, cy)
    }
  })
}

const toggleCollapse = (element, collapse, cy) => {
  if (collapse) {
    element.addClass('collapsed')
  } else {
    element.removeClass('collapsed')
  }

  element.outgoers('edge').forEach(edge => {
    toggleVisibility(edge, collapse)
  })

  if (collapse) {
    const orphans = cy.filter(e => {
      return e.isNode() && !e.hasClass('internal') && e.incomers('edge:visible').length === 0
    })
    orphans.forEach(o => {
      console.log('orphan ' + o.id())
      toggleVisibility(o, true)
      toggleChildVisibility(o, true)
    })
  } else {
    toggleChildVisibility(element, false)
  }
}

const toggleVisibility = (e, hide) => {
  if (hide) {
    e.addClass('hidden')
  } else {
    e.removeClass('hidden collapsed')
  }
}

const toggleChildVisibility = (e, hide) => {
  e.successors().forEach(s => {
    if (hide && s.isNode()) {
      s.addClass('hidden')
    } else if (!hide) {
      s.removeClass('hidden collapsed')
    }
  })
}

renderGraph()
