from __future__ import annotations

import importlib.util
import sys
import tempfile
import unittest
from pathlib import Path


IMPORTER_PATH = Path(__file__).parents[1] / "tools" / "import_tldr.py"
SPEC = importlib.util.spec_from_file_location("commandman_import_tldr", IMPORTER_PATH)
assert SPEC and SPEC.loader
IMPORTER = importlib.util.module_from_spec(SPEC)
sys.modules[SPEC.name] = IMPORTER
SPEC.loader.exec_module(IMPORTER)


class ImportTldrTests(unittest.TestCase):
    def test_directory_pages_only_loads_supported_platform_directories(self) -> None:
        with tempfile.TemporaryDirectory() as temporary_directory:
            root = Path(temporary_directory)
            for directory in ("common", "linux", "windows", "android"):
                (root / directory).mkdir()
                (root / directory / f"{directory}.md").write_text(
                    f"# {directory}\n\n- Example:\n\n`{directory}`\n",
                    encoding="utf-8",
                )

            pages = IMPORTER.directory_pages(root)

        self.assertEqual(
            {("common", "common.md"), ("linux", "linux.md"), ("windows", "windows.md")},
            set(pages),
        )

    def test_chinese_page_overlays_matching_examples_without_reducing_coverage(self) -> None:
        english = IMPORTER.parse_page(
            "# demo\n\n> English summary.\n\n"
            "- List all:\n\n`demo -a`\n\n"
            "- Show version:\n\n`demo --version`\n"
        )
        chinese = (
            "# demo\n\n> 中文摘要。\n\n"
            "- 列出全部：\n\n`demo -a`\n"
        )
        assert english

        localized = IMPORTER.localized_page(english, chinese)

        self.assertEqual("中文摘要。", localized.summary)
        self.assertEqual(2, len(localized.examples))
        self.assertEqual("列出全部", localized.examples[0].description)
        self.assertEqual("Show version", localized.examples[1].description)


if __name__ == "__main__":
    unittest.main()
