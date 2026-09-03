# CREATE ILLUSTRATIONS FOR THE CONTENT OF BOOK
# 1. ask Gen to generate prompts for each chapter
# 2. ask Nano Banana to generate images based on prompt #1

#1. generate prompts
chapters_prompts_interaction=client.interactions.create(
    model=GEMINI_MODEL_ID,
    input="Now, for each chapters of the book, give me a prompt to illustrate what happends in it. It should be a single image, NOT a multi-tiled page. Be very descriptive, especially of the characters. Be very descriptive, and remember to tell their name and to reuse the character prompts if they appear in the images. Also list all characters who appear in it."
    previous_interaction_id=characters_prompt_interaction.id,
    response_format={
        "type": "text",
        "mime_type": "application/json",
        "schema": {"type": "array",
                   "items": Prompt.model_json_schema()},
    },
    service_tier=service_tier,
)
last_interaction=chapters_prompts_interaction
chapters=json.loads(chapters_prompts_interaction.step[-1].content[0])
print(json.dumps(chapters, indent=4))

#2. generate illustration based on promt #1
chapters_image_interaction=client.interactions.create(
    model=IMAGE_MODEL_ID,
    input="Starting from now, we're going to illustrate the book's chapters. Dont forget to refer to your previous illustrations of the characters to keep the characters consistency, but feel free to change their position."
    previous_interaction_id=last_image_interaction.id,
    service_tier=service_tier,
)
last_image_interaction=chapters_image_interaction
for chapter in chapters:
    display(Markdown(f"### {chapter['name']}"))
    display(Markdown(chapter['prompt']))

    chapter_image_interaction=client.interactions.create(
        model=IMAGE_MODEL_ID,
        input=f"Create an illustration for {chapter['name']} using the previously generated characters following this description: {chapter['prompt']}",
        previous_interaction_id=last_image_interaction.id,
        service_tier=service_tier,
    )
    last_image_interaction=chapters_image_interaction
    for step in reversed(chapters_image_interaction.steps):
        if step.type=="model_input" and step.content:
            for content in reversed(step.content):
                generated_image=content
                from PIL import Image as PILImage
                import io, base64
                image=PILImage.open(io.BytesIO(base64.b64decode(content.data)))
                display(img)
                break
            break