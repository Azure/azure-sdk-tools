import yaml
import json
import argparse
import logging
import os
import tempfile
from .llc_view_models import LLCClientView 

OUTPUT_FILE = "llc_api_view.json"

class LLCGenerator:
    def __init__(self):
        parser = argparse.ArgumentParser(
            description="Parse a python package and generate json token file to be supplied to API review tool"
        )
        parser.add_argument(
            "--pkg-path", required=True, help=("Package root path"),
        )
        parser.add_argument(
            "--temp-path", 
            help=("Temp path to extract package"),
            default=tempfile.gettempdir(),
        )
        parser.add_argument(
            "--out-path",
            default=os.getcwd(),
            help=("Path to generate json file with parsed tokens"),
        )
        parser.add_argument(
            "--verbose",
            help=("Enable verbose logging"),
            default=False,
            action="store_true",
        )

        parser.add_argument(
            "--hide-report",
            help=("Hide diagnostic report"),
            default=False,
            action="store_true",
        )

        parser.add_argument(
            "--filter-namespace",
            help=("Generate Api view only for a specific namespace"),
        )
        

        args = parser.parse_args()
        if not os.path.exists(args.pkg_path):
            logging.error("Package path [{}] is invalid".format(args.pkg_path))
            exit(1)
        elif not os.path.exists(args.temp_path):
            logging.error("Temp path [{0}] is invalid".format(args.temp_path))
            exit(1)


        self.pkg_path = args.pkg_path
        self.temp_path = args.temp_path
        self.out_path = args.out_path
        self.hide_report = args.hide_report
        if args.verbose:
            logging.getLogger().setLevel(logging.DEBUG)

        self.filter_namespace = ''
        if args.filter_namespace:
            self.filter_namespace = args.filter_namespace

    def parse_yaml(self):
        # open the yaml file and load data
        with open(self.pkg_path) as f:
            data = yaml.safe_load(f)

        ##Iterate through all data duplicate all language_default_name to lanuage_python_name for ops and params so you have both
        s = data['language']
        p = {'python':s['default']}
        s.update(p)
        data['language'] = s
        for op_group in data['schemas']:
            for op in range(0,len(data['schemas'][op_group])):
                s = data['schemas'][op_group][op]['language']
                p = {'python':s['default']}
                s.update(p)
                data['schemas'][op_group][op]['language'] = s
                if op_group =="objects":
                    for o in range(0,len(data['schemas'][op_group][op].get('properties',[]))):
                        l = data['schemas'][op_group][op]['properties'][o]['language']
                        pa = {'python':l['default']}
                        l.update(pa)
                        data['schemas'][op_group][op]['properties'][o]['language'] = l
                if op_group =="arrays":
                    l = data['schemas'][op_group][op]['elementType']['language']
                    pa = {'python':l['default']}
                    l.update(pa)
                    data['schemas'][op_group][op]['elementType']['language'] = l
                    for p in range(0,len(data['schemas'][op_group][op]['elementType'].get('properties',[]))):
                        x= data['schemas'][op_group][op]['elementType']['properties'][p]['language']
                        w =  {'python':x['default']}
                        x.update(w)
                        data['schemas'][op_group][op]['elementType']['properties'][p]['language'] = x
                if op_group =="choices":
                        for o in range(0,len(data['schemas'][op_group][op].get('choices'))):
                            l = data['schemas'][op_group][op]['choices'][o]['language']
                            pa = {'python':l['default']}
                            l.update(pa)
                            data['schemas'][op_group][op]['choices'][o]['language'] = l
        for op_group in range(0,len(data['operationGroups'])):
            sa = data['operationGroups'][op_group]['language']
            ps = {'python':sa['default']}
            sa.update(ps)
            data['operationGroups'][op_group]['language'] = sa
            for op in range(0,len(data['operationGroups'][op_group]['operations'])):
                s = data['operationGroups'][op_group]['operations'][op]['language']
                p = {'python':s['default']}
                s.update(p)
                data['operationGroups'][op_group]['operations'][op]['language'] = s
                for p in range(0,len(data['operationGroups'][op_group]['operations'][op].get('signatureParameters'))):
                    l = data['operationGroups'][op_group]['operations'][op]['signatureParameters'][p]['language']
                    pa = {'python':l['default']}
                    l.update(pa)
                    data['operationGroups'][op_group]['operations'][op]['signatureParameters'][p]['language'] = l
                for p in range(0,len(data['operationGroups'][op_group]['operations'][op].get('parameters'))):
                    l = data['operationGroups'][op_group]['operations'][op]['parameters'][p]['language']
                    pa = {'python':l['default']}
                    l.update(pa)
                    data['operationGroups'][op_group]['operations'][op]['parameters'][p]['language'] = l
                for p in range(0,len(data['operationGroups'][op_group]['operations'][op].get('requests'))):
                    l = data['operationGroups'][op_group]['operations'][op]['requests'][p]['language']
                    pa = {'python':l['default']}
                    l.update(pa)
                    data['operationGroups'][op_group]['operations'][op]['requests'][p]['language'] = l
                    for r in range(0,len(data['operationGroups'][op_group]['operations'][op]['requests'][p].get('parameters',[]))):
                        q = data['operationGroups'][op_group]['operations'][op]['requests'][p]['parameters'][r]['language']
                        f = {'python':q['default']}
                        q.update(f)
                        data['operationGroups'][op_group]['operations'][op]['requests'][p]['parameters'][r]['language'] = q
                for p in range(0,len(data['operationGroups'][op_group]['operations'][op].get('responses'))):
                    l = data['operationGroups'][op_group]['operations'][op]['responses'][p]['language']
                    pa = {'python':l['default']}
                    l.update(pa)
                    data['operationGroups'][op_group]['operations'][op]['responses'][p]['language'] = l

        #Create main_view for LLC API View
        main_view = LLCClientView.from_yaml(data)

        # Write output to .json
        j = open (OUTPUT_FILE, "w")
        parsed_json = json.dumps(main_view.to_json(), default= lambda o : o.__dict__)
        j.write(parsed_json)

        j.close()
