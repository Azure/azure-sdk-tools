const renderGraph = () => {
  const config = {
    container: document.getElementById('cy'),
    elements: [],
    autounselectify: true,

    layout: {
      name: 'dagre',
      ranker: 'tight-tree',
      nodeSep: 10,
      rankSep: 400,
      padding: 4
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
        selector: '.internalbinary',
        style: {
          'background-color': '#fb7'
        }
      },
      {
        selector: '.collapsed',
        style: {
          'background-color': '#7bf'
        }
      },
      {
        selector: 'node.highlight',
        style: {
          'border-color': '#f77',
          'border-width': '4px'
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
      },
      {
        selector: 'edge.highlight',
        style: {
          'line-color': '#f77',
          'target-arrow-color': '#f77',
          'width': '3px'
        }
      }
    ]
  }

  // Add the nodes
  for (const pkg of Object.keys(data)) {
    config.elements.push({
      data: {
        id: pkg,
        label: `${data[pkg].name}\n${data[pkg].version}`
      },
      classes: data[pkg].type
    })
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

  cy.on('mouseover', 'node', event => {
    const element = event.target
    element.addClass('highlight')
    if (!element.hasClass('collapsed')) {
      element.outgoers().addClass('highlight')
    }
    element.incomers().forEach(e => {
      if (!e.hasClass('collapsed')) {
        e.addClass('highlight')
      }
    })
  })

  cy.on('mouseout', 'node', event => {
    const element = event.target
    element.removeClass('highlight')
    element.outgoers().removeClass('highlight')
    element.incomers().removeClass('highlight')
  })

  document.addEventListener('keydown', event => {
    if (event.key === '-') {
      cy.nodes('.internal').forEach(node => {
        if (!node.hasClass('collapsed') && !node.hasClass('hidden')) {
          triggerCollapse(cy, node, true)
        }
      })
    } else if (event.key === '=') {
      cy.nodes('.internal').forEach(node => {
        triggerCollapse(cy, node, false)
      })
    }
  })

  cy.on('tap', 'node', event => {
    const element = event.target
    const collapse = !element.hasClass('collapsed')
    triggerCollapse(cy, element, collapse)
    element.emit('mouseout')
    element.emit('mouseover')
  })
}

const triggerCollapse = (cy, element, collapse) => {
  if (element.outgoers().length === 0) { return }

  if (collapse) {
    element.addClass('collapsed')
  } else {
    element.removeClass('collapsed')
  }

  element.outgoers('edge').forEach(edge => {
    toggleElementVisibility(edge, !collapse)
  })

  if (collapse) {
    const orphans = cy.filter(e => {
      return e.isNode() && !e.hasClass('internal') && e.incomers('edge:visible').length === 0
    })
    orphans.forEach(o => {
      console.log('orphan ' + o.id())
      toggleElementVisibility(o, false)
      toggleChildVisibility(o, false)
    })
  } else {
    toggleChildVisibility(element, true)
  }
}

const toggleElementVisibility = (e, visible) => {
  if (!visible) {
    e.addClass('hidden')
  } else {
    e.removeClass('hidden collapsed')
  }
}

const toggleChildVisibility = (e, visible) => {
  e.successors().forEach(s => {
    if (!visible && s.isNode()) {
      s.addClass('hidden')
    } else if (visible) {
      s.removeClass('hidden collapsed')
    }
  })
}

renderGraph()
