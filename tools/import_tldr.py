"""Build Command Man's compact offline index from a tldr-pages checkout."""

from __future__ import annotations

import argparse
import json
import re
from dataclasses import dataclass
from pathlib import Path


SUPPORTED_DIRECTORIES = {"common", "linux", "windows"}
KNOWN_ALIASES = {
    "ls": ["ll"],
}


@dataclass(frozen=True)
class Example:
    description: str
    command: str
    search_terms: list[str]
    comparison_key: str


@dataclass(frozen=True)
class Page:
    name: str
    summary: str
    examples: list[Example]


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser()
    parser.add_argument(
        "--english-root",
        type=Path,
        required=True,
        help="Path to the upstream pages directory",
    )
    parser.add_argument(
        "--chinese-root",
        type=Path,
        required=True,
        help="Path to the upstream pages.zh directory",
    )
    parser.add_argument("--output", type=Path, required=True)
    return parser.parse_args()


def directory_pages(root: Path) -> dict[tuple[str, str], str]:
    if not root.is_dir():
        raise FileNotFoundError(f"TLDR page directory does not exist: {root}")

    pages: dict[tuple[str, str], str] = {}
    for directory in sorted(SUPPORTED_DIRECTORIES):
        directory_root = root / directory
        if not directory_root.is_dir():
            continue

        for path in sorted(directory_root.glob("*.md")):
            pages[(directory, path.name.lower())] = path.read_text(encoding="utf-8-sig")
    return pages


def render_placeholder(content: str) -> str:
    if content.startswith("[") and content.endswith("]"):
        variants = content[1:-1].split("|", 1)
        return variants[0]
    return f"<{content}>"


def render_command(raw: str) -> str:
    rendered = raw.replace(r"\{\{", "\uFFF0").replace(r"\}\}", "\uFFF1")

    # Handle placeholders such as {{stash@{0}}} before the general form.
    rendered = re.sub(
        r"\{\{([^{}]*\{[^{}]*\})\}\}",
        lambda match: render_placeholder(match.group(1)),
        rendered,
    )
    rendered = re.sub(
        r"\{\{(.*?)\}\}",
        lambda match: render_placeholder(match.group(1)),
        rendered,
    )
    return rendered.replace("\uFFF0", "{{").replace("\uFFF1", "}}")


def search_terms(raw_command: str) -> list[str]:
    return sorted(
        set(re.findall(r"(?<![\w])--?[A-Za-z0-9][A-Za-z0-9-]*", raw_command)),
        key=str.casefold,
    )


def parse_page(markdown: str) -> Page | None:
    lines = markdown.replace("\r\n", "\n").split("\n")
    title = next((line[2:].strip() for line in lines if line.startswith("# ")), "")
    if not title:
        return None

    summary_lines = [
        line[2:].strip()
        for line in lines
        if line.startswith("> ")
        and "more information:" not in line.casefold()
        and "更多信息" not in line
    ]
    summary = " ".join(summary_lines).strip()

    examples: list[Example] = []
    for index, line in enumerate(lines):
        if not line.startswith("- "):
            continue

        description = line[2:].strip().removesuffix(":").removesuffix("：")
        raw_command = ""
        for candidate in lines[index + 1 : index + 5]:
            match = re.fullmatch(r"`(.+)`", candidate.strip())
            if match:
                raw_command = match.group(1)
                break
            if candidate.startswith("- ") or candidate.startswith("# "):
                break

        if not raw_command:
            continue

        rendered = render_command(raw_command)
        examples.append(
            Example(
                description=description,
                command=rendered,
                search_terms=search_terms(raw_command),
                comparison_key=re.sub(r"\s+", " ", rendered).casefold(),
            )
        )

    if not examples:
        return None
    return Page(name=title, summary=summary, examples=examples)


def platform_for(directory: str, page: Page) -> str:
    if page.name.casefold().startswith("git "):
        return "Git"
    if directory == "windows":
        return "Windows"
    if directory == "linux":
        return "Linux"
    return "Common"


def localized_page(english: Page, chinese_markdown: str | None) -> Page:
    if not chinese_markdown:
        return english
    chinese = parse_page(chinese_markdown)
    if chinese is None:
        return english

    descriptions = {
        example.comparison_key: example.description for example in chinese.examples
    }
    examples = [
        Example(
            description=descriptions.get(example.comparison_key, example.description),
            command=example.command,
            search_terms=example.search_terms,
            comparison_key=example.comparison_key,
        )
        for example in english.examples
    ]
    return Page(
        name=english.name,
        summary=chinese.summary or english.summary,
        examples=examples,
    )


def command_entry(directory: str, filename: str, page: Page) -> dict[str, object]:
    platform = platform_for(directory, page)
    examples = [
        {
            "command": example.command,
            "description": example.description,
            "searchTerms": example.search_terms,
        }
        for example in page.examples
    ]
    page_key = Path(filename).stem
    aliases = KNOWN_ALIASES.get(page.name.casefold(), [])
    return {
        "id": f"tldr-{platform.casefold()}-{page_key}",
        "name": page.name,
        "platform": platform,
        "summary": page.summary or f"{page.name} command examples",
        "aliases": aliases,
        "keywords": ["tldr", page_key],
        "source": "TLDR Pages",
        "simple": examples[:2],
        "full": examples,
    }


def main() -> None:
    args = parse_args()
    english_pages = directory_pages(args.english_root)
    chinese_pages = directory_pages(args.chinese_root)

    commands: list[dict[str, object]] = []
    seen_ids: set[str] = set()
    for (directory, filename), markdown in sorted(english_pages.items()):
        english = parse_page(markdown)
        if english is None:
            continue
        page = localized_page(english, chinese_pages.get((directory, filename)))
        entry = command_entry(directory, filename, page)
        identifier = str(entry["id"])
        if identifier in seen_ids:
            raise ValueError(f"Duplicate generated command id: {identifier}")
        seen_ids.add(identifier)
        commands.append(entry)

    args.output.parent.mkdir(parents=True, exist_ok=True)
    args.output.write_text(
        json.dumps(commands, ensure_ascii=False, separators=(",", ":")),
        encoding="utf-8",
    )
    print(f"Generated {len(commands)} commands at {args.output}")


if __name__ == "__main__":
    main()
