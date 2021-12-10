import { VirtualServerRequest } from './models'
import { getPath, getPureUrl, isManagementUrlLevel } from '../common/utils'

export class ResourceNode {
    public children: Record<string, ResourceNode>
    public exist: boolean
    constructor(public name?: string, public url?: string, public body?: string) {
        this.children = {}
        this.exist = false
    }
}

export class ResourcePool {
    public resourceRoot: ResourceNode
    public cascadeEnabled: boolean
    constructor(cascadeEnabled: boolean) {
        this.resourceRoot = new ResourceNode()
        this.cascadeEnabled = cascadeEnabled
    }

    public static isCreateMethod(req: VirtualServerRequest) {
        const method = req.method.toUpperCase()
        return ['PUT', 'POST'].indexOf(method) >= 0
    }

    public static isDeleteMethod(req: VirtualServerRequest) {
        return 'DELETE' === req.method.toUpperCase()
    }

    public updateResourcePool(req: VirtualServerRequest): boolean {
        const url = getPureUrl(req.url)
        const path = getPath(url)
        if (ResourcePool.isCreateMethod(req)) {
            return this.addResource(
                this.resourceRoot,
                path,
                req.url,
                path[path.length - 1],
                req.body,
                1
            )
        }
        if (ResourcePool.isDeleteMethod(req)) {
            return this.deleteResource(this.resourceRoot, path)
        }
        return true
    }

    public addResource(
        node: ResourceNode,
        path: string[],
        url: string,
        name: string,
        body: any,
        level: number
    ): boolean {
        if (path.length === 0) {
            node.url = url
            node.name = name
            node.body = body
            node.exist = true
            return true
        }
        const _name = path[0].toLowerCase()
        if (!(_name in node.children)) {
            if (this.cascadeEnabled && isManagementUrlLevel(level, url) && path.length > 1) {
                return false
            }
            node.children[_name] = new ResourceNode()
        } else {
            if (
                this.cascadeEnabled &&
                isManagementUrlLevel(level, url) &&
                path.length > 1 &&
                !node.children[_name].exist
            ) {
                return false
            }
        }
        return this.addResource(node.children[_name], path.slice(1), url, name, body, level + 1)
    }

    public hasUrl(req: VirtualServerRequest): boolean {
        const url: string = getPureUrl(req.url) as string
        const pathNames = getPath(url)
        const node = ResourcePool.getResource(this.resourceRoot, pathNames)
        return (
            node !== undefined &&
            (!this.cascadeEnabled || node.exist || isManagementUrlLevel(pathNames.length, url))
        )
    }

    public static isListUrl(req: VirtualServerRequest): boolean {
        const url: string = getPureUrl(req.url) as string
        return url.split('/').slice(1).length % 2 === 1
    }

    public static getResource(node: ResourceNode, path: string[]): ResourceNode | undefined {
        if (path.length === 0) return node
        const name = path[0].toLowerCase()
        if (name in node.children)
            return ResourcePool.getResource(node.children[name], path.slice(1))
        return undefined
    }

    public deleteResource(node: ResourceNode, path: string[]): boolean {
        if (path.length === 0) return true
        const _name = path[0].toLowerCase()
        if (_name in node.children) {
            if (path.length === 1) {
                node.children[_name].exist = false
                if (
                    Object.keys(node.children[_name].children).length === 0 ||
                    this.cascadeEnabled
                ) {
                    delete node.children[_name]
                }
                return true
            } else {
                return this.deleteResource(node.children[_name], path.slice(1))
            }
        }
        return true
    }
}
