import {
    BaseHttpController,
    HttpResponseMessage,
    JsonContent,
    controller,
    httpGet
} from 'inversify-express-utils'
import { logger } from '../common/utils'

@controller('/')
export class MetadataController extends BaseHttpController {
    constructor() {
        super()
    }
    @httpGet('metadata/endpoints')
    public async mockMetadataEndpints() {
        logger.info('fetching metadata')
        const ret = {
            galleryEndpoint: '',
            graphEndpoint: 'https://graph.chinacloudapi.cn/',
            // "graphEndpoint": "https://localhost:8443",
            portalEndpoint: '',
            authentication: {
                // "loginEndpoint": "https://localhost:8443", // "https://login.chinacloudapi.cn/",
                loginEndpoint: 'https://login.chinacloudapi.cn/',
                audiences: [
                    // "http://localhost:8081",
                    'https://management.core.chinacloudapi.cn/',
                    'https://management.chinacloudapi.cn/'
                ]
            }
        }
        const response = new HttpResponseMessage(200)
        response.content = new JsonContent(ret)
        return response
    }
}
