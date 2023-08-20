import { useState, useEffect } from 'react';
import { Alert, Button, Form } from 'react-bootstrap';
import axios from 'axios';

export default function leaderModule() {

	const [isLoading, setIsLoading] = useState(true);
	const [discord, setDiscord] = useState('');
	const [transferName, setTransferName] = useState('');
	const [discordSuccess, setDiscordSuccess] = useState(false);
	const [transferSuccess, setTransferSuccess] = useState(false);

	const transferLeadership = () => {
		setIsLoading(true);
		axios
			.post('/TransferGuildLeadership?name=' + transferName)
			.then(x => setTransferSuccess(true))
			.finally(() => setIsLoading(false));
	};
	const updateDiscord = () => {
		setIsLoading(true);
		setDiscordSuccess(false);
		axios
			.post('/GuildDiscord?webhook=' + encodeURIComponent(discord))
			.then(x => setDiscordSuccess(true))
			.finally(() => setIsLoading(false));
	};
	const getDiscord = () => {
		axios
			.get('/GetDiscordWebhook')
			.then(x => setDiscord(x.data))
			.finally(() => setIsLoading(false));
	};
	useEffect(getDiscord, []);

	return (
		<Alert variant='dark'>
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Transfer Guild Leadership</Form.Label>
					<Form.Control type="text" placeholder='Enter new guild leader name' value={transferName} onChange={e => setTransferName(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={isLoading || transferName.length < 4} onClick={transferLeadership}>Transfer</Button>
				{transferSuccess &&
					<Alert variant='success'>Successfully transferred leadership</Alert>
				}
			</Form>
			<hr />
			<Form onSubmit={e => e.preventDefault()}>
				<Form.Group>
					<Form.Label>Discord Webhook URL</Form.Label>
					<Form.Control type="text" placeholder='Enter Discord webhook URL' value={discord} onChange={e => setDiscord(e.target.value)} />
				</Form.Group>
				<br />
				<Button variant='warning' disabled={isLoading || discord.length < 10} onClick={updateDiscord}>Update</Button>
				{discordSuccess &&
					<Alert variant='success'>Successfully updated discard webhook URL</Alert>
				}
			</Form>
		</Alert>
	);
}