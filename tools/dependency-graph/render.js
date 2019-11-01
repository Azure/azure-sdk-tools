const renderGraph = (data) => {
  const config = {
    container: document.getElementById('cy'),
    elements: [],
    autounselectify: true,

    layout: {
      name: 'dagre',
      ranker: 'tight-tree',
      nodeSep: 10,
      rankSep: 400,
      padding: 10
    },
  
    style: [
      {
        selector: 'node',
        style: {
          'background-color': '#fff',
          'border-color': '#333',
          'border-width': '1px',
          'height': 'label',
          'label': 'data(label)',
          'padding': '8px',
          'shape': 'round-rectangle',
          'text-halign': 'center',
          'text-valign': 'center',
          'text-wrap': 'wrap',
          'width': 'label'
        }
      },
      {
        selector: 'node.internal',
        style: {
          'background-color': '#7f7'
        }
      },
      {
        selector: 'node.internalbinary',
        style: {
          'background-color': '#fb7'
        }
      },
      {
        selector: 'node.collapsed',
        style: {
          'background-color': '#7bf'
        }
      },
      {
        selector: 'node.search',
        style: {
          'background-color': '#ff7',
          'border-width': '6px'
        }
      },
      {
        selector: 'node.highlight',
        style: {
          'background-color': '#fff',
          'border-color': '#f77',
          'border-width': '6px'
        }
      },
      {
        selector: 'node.highlight.internal',
        style: {
          'background-color': '#7f7'
        }
      },
      {
        selector: 'node.highlight.internalbinary',
        style: {
          'background-color': '#fb7'
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
          'curve-style': 'bezier',
          'line-color': '#333',
          'target-arrow-color': '#333',
          'target-arrow-shape': 'triangle',
          'width': '1.5px'
        }
      },
      {
        selector: 'edge.highlight',
        style: {
          'line-color': '#f77',
          'target-arrow-color': '#f77',
          'width': '6px'
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
    if (document.activeElement.id === 'search') { return }

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

  let searchTerm = ''
  document.getElementById('search').addEventListener('input', event => {
    const newValue = event.target.value
    if (searchTerm !== newValue) {
      searchTerm = newValue
      cy.nodes().removeClass('search')
      if (searchTerm.length > 0) {
        const matches = cy.nodes(`[label *= '${searchTerm}']`)
        matches.addClass('search')
        document.getElementById('matches').innerText = `Matches: ${matches.length}`
      } else {
        document.getElementById('matches').innerText = ''
      }
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
