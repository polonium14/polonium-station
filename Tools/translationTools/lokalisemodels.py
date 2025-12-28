# Prawa autorskie (C) 2025 Polonium Statiom
#
# Ten program jest wolnym oprogramowaniem: można go rozpowszechniać i/lub modyfikować
# zgodnie z warunkami licencji GNU AGPL opublikowanej przez
# Free Software Foundation, w wersji 3 licencji lub
# w dowolnej późniejszej wersji.
#
# Ten program stworzony na podstawie kodu projektu Corvax,
# pierwotnie licencjonowanego na podstawie licencji MIT (patrz https://github.com/space-syndicate/space-station-14/blob/master/LICENSE.TXT).

import typing
import os
from pydash import py_
from project import Project

class LocalePath:
    def __init__(self, relative_file_path):
        self.pl = os.path.join(Project().pl_locale_dir_path, relative_file_path)
        self.en = os.path.join(Project().en_locale_dir_path, relative_file_path)


class LokaliseTranslation:
    def __init__(self, data, key_name: typing.AnyStr):
        self.key_name = key_name,
        self.data = data

class LokaliseKey:
    def __init__(self, data):
        self.data = data
        self.key_name = self.data.key_name['web']
        self.key_base_name = self.get_key_base_name(self.key_name)
        self.is_attr = self.check_is_attr()

    def get_file_path(self):

        relative_dir_path = '{relative_file_path}.ftl'.format(
            relative_file_path='/'.join(self.data.key_name['web'].split('.')[0].split('::')))

        return LocalePath(relative_dir_path)

    def get_key_base_name(self, key_name):
        splitted_name = key_name.split('.')
        return splitted_name[0]

    def get_key_last_name(self, key_name):
        splitted_name = key_name.split('.')
        return py_.last(splitted_name)

    def get_parent_key(self):
        if self.is_attr:
            splitted_name = self.key_name.split('.')[0:-1]
            return '.'.join(splitted_name)
        else:
            return None

    def check_is_attr(self):
        return len(self.key_name.split('.')) > 2

    def serialize(self):
        if self.is_attr:
            return self.serialize_attr()
        else:
            return self.serialize_message()



    def serialize_attr(self):
        return '.{name} = {value}'.format(name=self.get_key_last_name(self.key_name), value=self.get_translation('pl').data['translation'])

    def serialize_message(self):
        return '{name} = {value}'.format(name=self.get_key_last_name(self.key_name), value=self.get_translation('pl').data['translation'])

    def get_translation(self, language_iso='pl'):
        return list(map(lambda data: LokaliseTranslation(key_name=self.data.key_name['web'], data=data), py_.filter(self.data.translations, {'language_iso': language_iso})))[0]
