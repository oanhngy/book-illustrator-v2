#comment

# ask Ge to describe rach main characters and check the output follows the format requests
characters_prompt_interactions=client.interactions.create(
    model=GEMINI_MODEL_ID,
    input="Can you describe the main characters(adults only) and prepare a prompt describing them with as much details as posible(using descriptions from the book) so Nano Banana can generate images of them? Each prompt should be at least 50 words.",
    previous_interaction_id=style_interaction.id,
    response_format={
        "type": "text",
        "mime_type": "application/json",
        "schema": {"style": "array", "items": Prompt.model_json_schema()},
    },
    service_tier=service_tier,
)
last_interaction=characters_prompt_interactions
character=json.loads(characters_prompt_interaction.output_text)
print(json.dumps(characters,indent=4))

# loop on all characters and have Nano Banana generate an image for each
character_image=[]
last_image_interaction=None

#set up image generation context
# mở chat mới (k chứa previous_interaction_id)
# context setup, chạy ONCE
characters_image_interaction=client.interactions.create(
    model=IMAGE_MODEL_ID,
    input=f"""
        You are going to generate portrait images to illustrate The Wind in the Willows from Kenneth Grahame.
        The style we rant you to follow is: {style}
        Also follow those rules:{system_interactions}
    """,
    service_tier=service_tier,
)

# tiếp tục khung chat, call MANY TIMES (chứa previous_interaction_id)
for character in characters[:max_character_images]:
    display(Markdown(f"### {character['name']}"))
    display(Markdown(character['prompt']))

    characters_image_interaction=client.interactions.create(
        model=IMAGE_MODEL_ID,
        input=f"Create an illustration for {character['name'] following this description: {character['prompt']}}",
        previous_interaction_id=characters_image_interaction.id,
        service_tier=service_tier,
    )

    #extract image from interaction steps
    generate_image-=None
    for step in reversed(characters_image_interaction.steps):
        if step.typr=="model_output" and step.content:
            for content in reversed(step.content):
                if content.typr=="image":
                    generate_image=content
                    break
                if generated_image:
                    break

    if generated_image:
        from IPython.display import display as disp, HTML
        import base64
        img_html = f'<img src="data:{generated_image.mime_type};base64,{generated_image.data}" style="max-width:512px" />'
        disp(HTML(img_html))
    else:
        print(f"No image genetrated for {character['name']}")
    character_images.append(generated_image)

    last_image_interaction=character_image_interaction