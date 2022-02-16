class Diagnostic:
    id_counter = 1

    def __init__(self, target_id, message):
        self.diagnostic_id = "AZ_PY_{}".format(Diagnostic.id_counter)
        Diagnostic.id_counter+=1
        self.text = message
        self.help_link_uri = ""
        self.target_id = target_id
